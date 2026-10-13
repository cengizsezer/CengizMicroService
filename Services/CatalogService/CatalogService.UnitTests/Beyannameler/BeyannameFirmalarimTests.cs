using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Features.Declarations.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Beyannameler
{
    /// <summary>
    /// Beyannameler firmayı Firmalarım'dan (dbo.Firmalar, Declaration.FirmaId) alır; eski CustomerCompanies okunmaz.
    /// Takip ve Özet aynı kural: pasif firmanın kayıtları varsayılan gizli, istenirse görünür; FirmaId'si boş eski
    /// kayıtlar her zaman "Eşleşmeyen firma"da. Yeni ve düzenlenen kayıt FirmaId ile yazılır.
    ///
    /// VKN eşlemesi (AddDeclarationFirmaId migration'ındaki UPDATE) SQL'dir; InMemory'de çalışmaz — SQL Server'da
    /// boş ve verili deneme veritabanında ayrıca sınandı (rapor).
    /// </summary>
    public class BeyannameFirmalarimTests
    {
        private const int Lime = 5, Pkf = 4, Dgr = 3;
        private const int Yil = 2026, Ay = 9;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Lime, Unvan = "LIME TEKSTİL LTD. ŞTİ.", KisaAd = "LIME", VergiKimlikNo = "2222222222", Aktif = true },
                new Firma { Id = Dgr, Unvan = "DGR İÇ VE DIŞ TİC. A.Ş.", KisaAd = "", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Pkf, Unvan = "PKF ADAY BAĞIMSIZ DENETİM A.Ş.", KisaAd = "PKF ADAY DENETIM", VergiKimlikNo = "0070511435", Aktif = false });

            // Eski tablo: Takip/Özet artık OKUMAMALI — burada başka bir adla duruyor, ekranda görünmemeli.
            db.CustomerCompanies.Add(new CustomerCompany { Id = 24, TenantNo = "500", CompanyName = "PKF ADAY ESKİ AD", TaxNumber = "0070511435", IsActive = true });

            db.Declarations.AddRange(
                Kayit(1, Lime, "LIME", "0015 KDV-1", 100m),
                Kayit(2, Pkf, "PKF ADAY BAGIMSIZ DENETIM A.S", "0015 KDV-1", 200m, customerCompanyId: 24),
                Kayit(3, null, "ESKİ FİRMA A.Ş.", "0015 KDV-1", 300m, customerCompanyId: 31),
                Kayit(4, Lime, "LIME", "4017 KDV-2", 50m));
            db.BeyannameTurleri.AddRange(
                new BeyannameTuru { Id = 1, Deger = "0015 KDV-1", Kod = "0015", Ad = "KDV", Sira = 10, Aktif = true },
                new BeyannameTuru { Id = 2, Deger = "4017 KDV-2", Kod = "4017", Ad = "KDV-2", Sira = 20, Aktif = true });
            db.SaveChanges();
            return db;
        }

        private static Declaration Kayit(int id, int? firmaId, string ad, string tur, decimal tutar, int customerCompanyId = 0) => new()
        {
            Id = id, FirmaId = firmaId, CompanyName = ad, CustomerCompanyId = customerCompanyId, TenantNo = "",
            DeclarationType = tur, Year = Yil, Month = Ay, Amount = tutar, DueDate = new DateTime(Yil, Ay, 26)
        };

        // ---- Takip ----

        [Fact]
        public async Task Takip_pasif_firmanin_kaydi_varsayilan_gizli_eslesmeyen_her_zaman_gorunur()
        {
            using var db = Context();
            var gruplar = await new DeclarationQueryService(db).GetMonthlySummaryAsync(Yil, Ay);

            Assert.Equal(new[] { "LIME", BeyannameFirmasi.EslesmeyenAdi }, gruplar.Select(g => g.CompanyName));
            Assert.DoesNotContain(gruplar, g => g.FirmaId == Pkf);

            var lime = gruplar[0];
            Assert.Equal(Lime, lime.FirmaId);
            Assert.Equal(2, lime.DeclarationCount);
            Assert.False(lime.FirmaPasif);

            // Eşleşmeyen grup sonda; kayıt eski CompanyName'iyle.
            var eslesmeyen = gruplar[1];
            Assert.True(eslesmeyen.Eslesmeyen);
            Assert.Null(eslesmeyen.FirmaId);
            Assert.Equal("ESKİ FİRMA A.Ş.", Assert.Single(eslesmeyen.Declarations).CompanyName);
        }

        [Fact]
        public async Task Takip_pasifleri_goster_ile_pasif_firma_isaretli_gorunur_ad_Firmalarimdan()
        {
            using var db = Context();
            var gruplar = await new DeclarationQueryService(db).GetMonthlySummaryAsync(Yil, Ay, pasifleriGoster: true);

            var pkf = gruplar.Single(g => g.FirmaId == Pkf);
            Assert.True(pkf.FirmaPasif);
            Assert.Equal("PKF ADAY DENETIM", pkf.CompanyName);     // Firmalarım adı, eski tablonun değil
            Assert.DoesNotContain(gruplar, g => g.CompanyName == "PKF ADAY ESKİ AD");
            Assert.Equal(3, gruplar.Count);
            Assert.True(gruplar.Last().Eslesmeyen);
        }

        [Fact]
        public async Task Takip_firma_filtresi_FirmaId_ile()
        {
            using var db = Context();
            var gruplar = await new DeclarationQueryService(db).GetMonthlySummaryAsync(Yil, Ay, firmaId: Lime);

            var g = Assert.Single(gruplar);
            Assert.Equal(Lime, g.FirmaId);
            Assert.All(g.Declarations, d => Assert.Equal(Lime, d.FirmaId));
        }

        // ---- Kayıt ----

        [Fact]
        public async Task Yeni_kayit_FirmaId_ile_yazilir_ad_firmadan_eski_bag_sifir()
        {
            using var db = Context();
            var id = await new DeclarationCommandService(db).CreateAsync(new CreateDeclarationRequest
            {
                FirmaId = Dgr, CompanyName = "istemcinin yazdığı", CustomerCompanyId = 24,
                DeclarationType = "0015 KDV-1", Year = Yil, Month = Ay, Amount = 10m, DueDate = new DateTime(Yil, Ay, 26)
            });

            var k = await db.Declarations.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(Dgr, k.FirmaId);
            Assert.Equal("DGR İÇ VE DIŞ TİC. A.Ş.", k.CompanyName);   // kısa ad boş → unvan
            Assert.Equal(0, k.CustomerCompanyId);
        }

        [Fact]
        public async Task Yeni_kayit_pasif_ya_da_bos_firmaya_yapilmaz()
        {
            using var db = Context();
            var servis = new DeclarationCommandService(db);
            CreateDeclarationRequest Istek(int? firma) => new() { FirmaId = firma, DeclarationType = "0015 KDV-1", Year = Yil, Month = Ay };

            var pasif = await Assert.ThrowsAsync<BeyannameKuralException>(() => servis.CreateAsync(Istek(Pkf)));
            Assert.Contains("pasif", pasif.Message);
            await Assert.ThrowsAsync<BeyannameKuralException>(() => servis.CreateAsync(Istek(null)));
            await Assert.ThrowsAsync<BeyannameKuralException>(() => servis.CreateAsync(Istek(999)));
            Assert.Equal(4, await db.Declarations.CountAsync());
        }

        [Fact]
        public async Task Duzenleme_pasif_firmanin_kaydi_kendi_firmasiyla_olur_pasif_firmaya_tasinamaz()
        {
            using var db = Context();
            var servis = new DeclarationCommandService(db);
            UpdateDeclarationRequest Istek(int? firma) => new()
            {
                FirmaId = firma, DeclarationType = "0015 KDV-1", Year = Yil, Month = Ay, Amount = 250m, DueDate = new DateTime(Yil, Ay, 26)
            };

            await servis.UpdateAsync(2, Istek(Pkf));                  // pasif firmanın kaydı, firma aynı
            Assert.Equal(250m, (await db.Declarations.AsNoTracking().SingleAsync(x => x.Id == 2)).Amount);

            await Assert.ThrowsAsync<BeyannameKuralException>(() => servis.UpdateAsync(1, Istek(Pkf)));   // aktif → pasif

            await servis.UpdateAsync(3, Istek(Lime));                 // eşleşmeyen kayıt bir firmaya bağlanır
            var k = await db.Declarations.AsNoTracking().SingleAsync(x => x.Id == 3);
            Assert.Equal(Lime, k.FirmaId);
            Assert.Equal("LIME", k.CompanyName);
            Assert.Equal(31, k.CustomerCompanyId);                    // eski bağ olduğu gibi kalır
        }

        // ---- Özet ----

        [Fact]
        public async Task Ozet_satirlari_aktif_Firmalarim_firmalari_ve_eslesmeyen_satiri()
        {
            using var db = Context();
            var ozet = await new BeyannameOzetService(db).GetAsync(Yil, Ay);

            Assert.Equal(new[] { "DGR İÇ VE DIŞ TİC. A.Ş.", "LIME", BeyannameFirmasi.EslesmeyenAdi },
                         ozet.Satirlar.Select(s => s.FirmaAdi));
            Assert.DoesNotContain(ozet.Satirlar, s => s.FirmaId == Pkf);
            Assert.DoesNotContain(ozet.Satirlar, s => s.FirmaAdi == "PKF ADAY ESKİ AD");

            var lime = ozet.Satirlar.Single(s => s.FirmaId == Lime);
            Assert.Equal(2, lime.DoluHucreSayisi);
            var eslesmeyen = ozet.Satirlar.Last();
            Assert.True(eslesmeyen.Eslesmeyen);
            Assert.Equal(300m, eslesmeyen.ToplamTutar);
        }

        [Fact]
        public async Task Ozet_pasifleri_goster_ile_pasif_firma_satiri_isaretli()
        {
            using var db = Context();
            var ozet = await new BeyannameOzetService(db).GetAsync(Yil, Ay, pasifleriGoster: true);

            var pkf = ozet.Satirlar.Single(s => s.FirmaId == Pkf);
            Assert.True(pkf.FirmaPasif);
            Assert.Equal(200m, pkf.ToplamTutar);
        }

        [Fact]
        public async Task Ozet_eslesmeyen_kayit_yoksa_eslesmeyen_satiri_yok()
        {
            using var db = Context();
            db.Declarations.Remove(await db.Declarations.SingleAsync(x => x.Id == 3));
            await db.SaveChangesAsync();

            var ozet = await new BeyannameOzetService(db).GetAsync(Yil, Ay);
            Assert.DoesNotContain(ozet.Satirlar, s => s.Eslesmeyen);
        }
    }
}
