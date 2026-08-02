using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Features.FirmaKontrol.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.FirmaKontrol
{
    /// <summary>Kalem katalogu kuralları ve beyanname kaydetme davranışı.</summary>
    public class VergiBeyannameServiceTests
    {
        private const int FirmaId = 1;
        private const short Donem = 2026;

        private static CatalogContext YeniContext()
        {
            var options = new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase($"vergi-{Guid.NewGuid()}")
                .Options;

            return new CatalogContext(options, new FixedTenantAccessor("201"));
        }

        /// <summary>Bir firma ve test kalem katalogu ile dolu context.</summary>
        private static async Task<CatalogContext> HazirContextAsync()
        {
            var db = YeniContext();

            db.Firmalar.Add(new Firma { Id = FirmaId, Unvan = "Test A.Ş.", KisaAd = "Test" });
            db.VergiKalemleri.AddRange(VergiTestKatalogu.Olustur());
            await db.SaveChangesAsync();

            return db;
        }

        private static VergiBeyannameService Servis(CatalogContext db) => new(db);

        private static VergiBeyannameYazDto Beyanname(params (int KalemId, decimal Tutar)[] satirlar) => new()
        {
            DonemYil = Donem,
            TicariKar = 1_000_000m,
            KvOrani = 25m,
            AsgariKvHesapla = false,
            Satirlar = satirlar.Select(s => new VergiSatirYazDto { VergiKalemiId = s.KalemId, Tutar = s.Tutar }).ToList()
        };

        // ── Kalem katalogu ──

        [Fact]
        public async Task Kalemler_VarsayilanOlarakYalnizcaAktifleriDonuyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            await servis.KalemPasifeAlAsync(VergiTestKatalogu.IndirimBagis);

            var aktifler = await servis.GetKalemlerAsync();
            Assert.DoesNotContain(aktifler, k => k.Kod == "IND-05");

            var hepsi = await servis.GetKalemlerAsync(pasifDahil: true);
            Assert.Contains(hepsi, k => k.Kod == "IND-05" && !k.Aktif);
        }

        [Fact]
        public async Task KalemEkleme_KullaniciKalemiOlarakEkleniyor()
        {
            using var db = await HazirContextAsync();

            var eklenen = await Servis(db).KalemEkleAsync(new VergiKalemiYazDto
            {
                Kod = "OZEL-01",
                Ad = "Özel indirim",
                Grup = VergiKalemGrubu.KazancVarsa,
                SiraNo = 90
            });

            Assert.False(eklenen.SistemKalemi);
            Assert.True(eklenen.Aktif);
            Assert.Equal("OZEL-01", eklenen.Kod);
        }

        [Fact]
        public async Task AyniKod_IkinciKezEklenemiyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            var hata = await Assert.ThrowsAsync<VergiKuralException>(
                () => servis.KalemEkleAsync(new VergiKalemiYazDto { Kod = "IND-05", Ad = "Kopya", Grup = VergiKalemGrubu.KazancVarsa }));

            Assert.Equal("kod", hata.Field);
        }

        [Theory]
        [InlineData("", "Ad", "kod")]
        [InlineData("KOD", "", "ad")]
        public async Task GecersizKalemGirdisi_KuralHatasiVeriyor(string kod, string ad, string alan)
        {
            using var db = await HazirContextAsync();

            var hata = await Assert.ThrowsAsync<VergiKuralException>(
                () => Servis(db).KalemEkleAsync(new VergiKalemiYazDto { Kod = kod, Ad = ad, Grup = VergiKalemGrubu.Kkeg }));

            Assert.Equal(alan, hata.Field);
        }

        // ── Kabul: sistem kaleminin kodu kilitli ──

        [Fact]
        public async Task SistemKalemi_KoduVeGrubuDegismiyor_AdiDegisiyor()
        {
            using var db = await HazirContextAsync();

            var guncel = await Servis(db).KalemGuncelleAsync(VergiTestKatalogu.IndirimBagis, new VergiKalemiYazDto
            {
                Kod = "DEGISTIRILDI",
                Ad = "Bağış ve yardımlar (güncel ad)",
                Grup = VergiKalemGrubu.Kkeg,
                SiraNo = 5
            });

            Assert.NotNull(guncel);
            Assert.Equal("IND-05", guncel!.Kod);                       // kod kilitli
            Assert.Equal(VergiKalemGrubu.KazancVarsa, guncel.Grup);    // grup kilitli
            Assert.Equal("Bağış ve yardımlar (güncel ad)", guncel.Ad); // ad serbest
        }

        [Fact]
        public async Task KullaniciKalemi_KoduVeGrubuDegistirilebiliyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            var eklenen = await servis.KalemEkleAsync(new VergiKalemiYazDto
            {
                Kod = "OZEL-02", Ad = "Özel", Grup = VergiKalemGrubu.KazancVarsa
            });

            var guncel = await servis.KalemGuncelleAsync(eklenen.Id, new VergiKalemiYazDto
            {
                Kod = "OZEL-02-YENI", Ad = "Özel (yeni)", Grup = VergiKalemGrubu.Kkeg
            });

            Assert.Equal("OZEL-02-YENI", guncel!.Kod);
            Assert.Equal(VergiKalemGrubu.Kkeg, guncel.Grup);
        }

        [Fact]
        public async Task SistemKalemi_Silinemiyor()
        {
            using var db = await HazirContextAsync();

            var sonuc = await Servis(db).KalemSilAsync(VergiTestKatalogu.IndirimBagis);

            Assert.Equal(KalemSilmeSonuc.SistemKalemi, sonuc);
        }

        [Fact]
        public async Task KullanilmisKalem_Silinemiyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            var eklenen = await servis.KalemEkleAsync(new VergiKalemiYazDto
            {
                Kod = "OZEL-03", Ad = "Özel", Grup = VergiKalemGrubu.KazancVarsa
            });

            await servis.KaydetAsync(FirmaId, Beyanname((eklenen.Id, 5_000m)));

            Assert.Equal(KalemSilmeSonuc.Kullanilmis, await servis.KalemSilAsync(eklenen.Id));
        }

        [Fact]
        public async Task KullanilmamisKullaniciKalemi_Silinebiliyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            var eklenen = await servis.KalemEkleAsync(new VergiKalemiYazDto
            {
                Kod = "OZEL-04", Ad = "Özel", Grup = VergiKalemGrubu.KazancVarsa
            });

            Assert.Equal(KalemSilmeSonuc.Silindi, await servis.KalemSilAsync(eklenen.Id));
        }

        [Fact]
        public async Task BagliIstisna_YalnizcaGrup2KalemiOlabiliyor()
        {
            using var db = await HazirContextAsync();

            var hata = await Assert.ThrowsAsync<VergiKuralException>(
                () => Servis(db).KalemEkleAsync(new VergiKalemiYazDto
                {
                    Kod = "OZEL-05",
                    Ad = "Hatalı bağ",
                    Grup = VergiKalemGrubu.Kkeg,
                    IstisnayaIliskinMi = true,
                    BagliIstisnaKalemiId = VergiTestKatalogu.MahsupGecici   // Grup 4
                }));

            Assert.Equal("bagliIstisnaKalemiId", hata.Field);
        }

        [Fact]
        public async Task Siralama_Kaydediliyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            await servis.SiralamayiKaydetAsync(new List<VergiKalemSiraDto>
            {
                new() { KalemId = VergiTestKatalogu.IndirimArge, SiraNo = 9 }
            });

            var kalem = await servis.GetKalemAsync(VergiTestKatalogu.IndirimArge);
            Assert.Equal((short)9, kalem!.SiraNo);
        }

        // ── Beyanname ──

        [Fact]
        public async Task Beyanname_KaydediliyorVeSonucHesaplaniyor()
        {
            using var db = await HazirContextAsync();

            var kayit = await Servis(db).KaydetAsync(FirmaId, Beyanname(
                (VergiTestKatalogu.KkegCeza, 100_000m),
                (VergiTestKatalogu.MahsupGecici, 50_000m)));

            Assert.Equal(FirmaId, kayit.FirmaId);
            Assert.Equal(Donem, kayit.DonemYil);
            Assert.Equal(1_100_000m, kayit.Sonuc.Matrah);
            Assert.Equal(275_000m, kayit.Sonuc.HesaplananVergi);
            Assert.Equal(225_000m, kayit.Sonuc.OdenecekVergi);
        }

        [Fact]
        public async Task Beyanname_AyniDonemdeUpsertEdiliyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            await servis.KaydetAsync(FirmaId, Beyanname((VergiTestKatalogu.KkegCeza, 100_000m)));
            var ikinci = await servis.KaydetAsync(FirmaId, Beyanname((VergiTestKatalogu.KkegCeza, 200_000m)));

            Assert.Equal(1, await db.VergiHesaplamalar.CountAsync());
            Assert.Equal(1_200_000m, ikinci.Sonuc.Matrah);
        }

        // ── Kabul: sıfır tutarlı kalemler saklanmıyor ──

        [Fact]
        public async Task SifirTutarliSatirlar_Saklanmiyor()
        {
            using var db = await HazirContextAsync();

            var kayit = await Servis(db).KaydetAsync(FirmaId, Beyanname(
                (VergiTestKatalogu.KkegCeza, 100_000m),
                (VergiTestKatalogu.IndirimBagis, 0m)));

            Assert.Single(kayit.Satirlar);
            Assert.Equal("KKEG-03", kayit.Satirlar[0].Kod);
        }

        [Fact]
        public async Task GecmisYilZarari_MahsupEdilenTutariMotordanYaziliyor()
        {
            using var db = await HazirContextAsync();

            var dto = Beyanname();
            dto.TicariKar = 300_000m;
            dto.GecmisYilZararlari = new List<GecmisYilZarariYazDto>
            {
                new() { ZararYili = 2024, ZararTutari = 500_000m }
            };

            var kayit = await Servis(db).KaydetAsync(FirmaId, dto);

            var zarar = Assert.Single(kayit.GecmisYilZararlari);
            Assert.Equal(300_000m, zarar.MahsupEdilen);   // kalan kazanç kadar
            Assert.Equal(0m, kayit.Sonuc.Matrah);
        }

        [Fact]
        public async Task OlmayanFirma_KaydetmedeHataVeriyor()
        {
            using var db = await HazirContextAsync();

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => Servis(db).KaydetAsync(9999, Beyanname()));
        }

        [Fact]
        public async Task GecersizVergiOrani_Reddediliyor()
        {
            using var db = await HazirContextAsync();

            var dto = Beyanname();
            dto.KvOrani = 0m;

            var hata = await Assert.ThrowsAsync<VergiKuralException>(() => Servis(db).KaydetAsync(FirmaId, dto));
            Assert.Equal("kvOrani", hata.Field);
        }

        [Fact]
        public async Task Onizleme_KaydetmedenHesapliyor()
        {
            using var db = await HazirContextAsync();

            var sonuc = await Servis(db).OnizleAsync(Beyanname((VergiTestKatalogu.KkegCeza, 100_000m)));

            Assert.Equal(1_100_000m, sonuc.Matrah);
            Assert.Equal(0, await db.VergiHesaplamalar.CountAsync());
        }

        [Fact]
        public async Task KayitliBeyanname_GeriOkunabiliyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            await servis.KaydetAsync(FirmaId, Beyanname((VergiTestKatalogu.KkegCeza, 100_000m)));

            var geri = await servis.GetBeyannameAsync(FirmaId, Donem);

            Assert.NotNull(geri);
            Assert.Equal(1_100_000m, geri!.Sonuc.Matrah);
            Assert.Single(geri.Satirlar);
        }

        [Fact]
        public async Task PasifKalemliGecmisBeyanname_HesaplanmayaDevamEdiyor()
        {
            using var db = await HazirContextAsync();
            var servis = Servis(db);

            await servis.KaydetAsync(FirmaId, Beyanname((VergiTestKatalogu.KkegCeza, 100_000m)));
            await servis.KalemPasifeAlAsync(VergiTestKatalogu.KkegCeza);

            var geri = await servis.GetBeyannameAsync(FirmaId, Donem);

            // Kalem pasife alınsa da kayıtlı beyanname bozulmamalı.
            Assert.Equal(1_100_000m, geri!.Sonuc.Matrah);
        }
    }
}
