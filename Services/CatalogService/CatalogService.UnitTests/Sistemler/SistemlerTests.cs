using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Sistemler;
using CatalogService.Api.Features.Sistemler.Domain;
using CatalogService.Api.Features.Sistemler.Dtos;
using CatalogService.Api.Features.Sistemler.Services;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Sistemler
{
    /// <summary>
    /// Kullanılan sistemler (Prompt 9): ortak liste, benzer ad uyarısı, birleştirme, pasife
    /// alma, firma atamaları ve ayrılan Sınıflandırma kartı.
    /// </summary>
    public class SistemlerTests
    {
        private const int Carriere = 5;
        private const int Dgr = 3;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Carriere, Unvan = "CARRIERE B.V. TR", KisaAd = "CARRIERE", VergiKimlikNo = "1234567890", Aktif = true,
                            OrkaFirmaKodu = "0521", MizanFormati = "Luca" },
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true });
            db.SaveChanges();
            SistemSeed.SeedAsync(db).GetAwaiter().GetResult();
            return db;
        }

        private static SistemService Servis(CatalogContext db) => new(db);

        private static int Id(CatalogContext db, string ad, SistemTuru? tur = null)
            => db.Sistemler.AsNoTracking().Single(s => s.Ad == ad && (tur == null || s.Tur == tur)).Id;

        private static Task<FirmaPaneliDto> Panel(CatalogContext db, int firmaId)
            => new FirmaPaneliService(db, TestSaati.Gun(new DateTime(2026, 10, 1))).PanelAsync(firmaId);

        // ---- Seed ----

        [Fact]
        public async Task Seed_yalniz_gercek_kayitlari_ekler_ve_idempotent()
        {
            using var db = Context();

            Assert.Equal(0, await SistemSeed.SeedAsync(db));
            Assert.Equal(new[] { "Logo", "Luca", "Mikro", "ORKA" },
                         db.Sistemler.Where(s => s.Tur == SistemTuru.Muhasebe).Select(s => s.Ad).OrderBy(a => a).ToArray());
            Assert.Equal(new[] { "DijitalPlanet", "TURMOB" },
                         db.Sistemler.Where(s => s.Tur == SistemTuru.EFatura).Select(s => s.Ad).OrderBy(a => a).ToArray());
            Assert.Equal(7, await db.Sistemler.CountAsync());
            Assert.Empty(db.Sistemler.Where(s => s.Tur == SistemTuru.Banka || s.Tur == SistemTuru.Bordro));
        }

        // ---- Benzer ad ----

        [Theory]
        [InlineData("Dijital Planet")]
        [InlineData("dijitalplanet")]
        [InlineData("DİJİTAL-PLANET")]
        [InlineData("DIJITAL_PLANET")]   // noktasız I: Türkçe küçültmede ı, yine aynı anahtar
        [InlineData("Dijital.Planet ")]
        [InlineData("DijitalPlanett")]   // tek harf yazım hatası
        public void Benzer_adlar_ayni_anahtara_iner(string ad)
            => Assert.True(SistemAdi.Benzer("DijitalPlanet", ad));

        [Theory]
        [InlineData("Uyumsoft")]
        [InlineData("TURMOB")]
        [InlineData("Lugo")]            // kısa adda tek harf farkı benzer sayılmaz (Logo ≠ Lugo)
        public void Farkli_adlar_benzer_degil(string ad)
            => Assert.False(SistemAdi.Benzer(ad == "Lugo" ? "Logo" : "DijitalPlanet", ad));

        [Fact]
        public async Task Yeni_ekle_benzer_adda_sorar_israr_edilirse_ekler()
        {
            using var db = Context();
            var servis = Servis(db);

            var ex = await Assert.ThrowsAsync<SistemBenzerException>(() =>
                servis.EkleAsync(new SistemEkleDto { Ad = "Dijital Planet", Tur = SistemTuru.EFatura }));
            Assert.Equal("DijitalPlanet", ex.Benzer.Ad);
            Assert.Contains("onu mu demek istediniz", ex.Message);
            Assert.Equal(2, await db.Sistemler.CountAsync(s => s.Tur == SistemTuru.EFatura)); // sormadan eklenmedi

            var eklenen = await servis.EkleAsync(new SistemEkleDto { Ad = "Dijital Planet", Tur = SistemTuru.EFatura, Israr = true });
            Assert.Equal("Dijital Planet", eklenen.Ad);

            // Birebir aynı ad (büyük/küçük harf hariç) ısrarla da açılmaz.
            await Assert.ThrowsAsync<SistemKuralException>(() =>
                servis.EkleAsync(new SistemEkleDto { Ad = "dijitalplanet", Tur = SistemTuru.EFatura, Israr = true }));
        }

        [Fact]
        public async Task Uyumsoft_uyarisiz_eklenir_ayni_ad_baska_turde_serbest()
        {
            using var db = Context();
            var servis = Servis(db);

            var uyumsoft = await servis.EkleAsync(new SistemEkleDto { Ad = " Uyumsoft ", Tur = SistemTuru.EFatura });
            var lucaBordro = await servis.EkleAsync(new SistemEkleDto { Ad = "Luca", Tur = SistemTuru.Bordro });

            Assert.Equal("Uyumsoft", uyumsoft.Ad);
            Assert.Equal(SistemTuru.Bordro, lucaBordro.Tur);
            Assert.Contains(await servis.ListeAsync(SistemTuru.EFatura, false), s => s.Ad == "Uyumsoft");
            Assert.DoesNotContain(await servis.ListeAsync(SistemTuru.EFatura, false), s => s.Tur == SistemTuru.Bordro);
        }

        [Fact]
        public async Task Yeniden_adlandirmada_da_benzer_ad_sorulur()
        {
            using var db = Context();
            var servis = Servis(db);
            var turmob = Id(db, "TURMOB");

            await Assert.ThrowsAsync<SistemBenzerException>(() =>
                servis.GuncelleAsync(turmob, new SistemGuncelleDto { Ad = "Dijital Planet" }));

            var sonuc = await servis.GuncelleAsync(turmob, new SistemGuncelleDto { Ad = "TÜRMOB e-Fatura" });
            Assert.Equal("TÜRMOB e-Fatura", sonuc.Ad);
        }

        // ---- Firma atamaları ----

        [Fact]
        public async Task Efatura_ve_iki_banka_portali_atanir_orka_kodu_ayni_alana_yazilir()
        {
            using var db = Context();
            var servis = Servis(db);
            var garanti = await servis.EkleAsync(new SistemEkleDto { Ad = "Garanti BBVA", Tur = SistemTuru.Banka });
            var isbank = await servis.EkleAsync(new SistemEkleDto { Ad = "İş Bankası", Tur = SistemTuru.Banka });

            await servis.FirmaSistemleriKaydetAsync(Carriere, new FirmaSistemleriKaydetDto
            {
                Atamalar =
                {
                    new() { SistemId = Id(db, "Luca"), FirmaKodu = " 0417 " },
                    new() { SistemId = Id(db, "DijitalPlanet") },
                    new() { SistemId = garanti.Id },
                    new() { SistemId = isbank.Id }
                },
                OrkaFirmaKodu = "0001",
                MizanFormati = "Luca",
                SistemNotu = "DijitalPlanet şifresi firma yetkilisinde"
            });

            var panel = (await Panel(db, Carriere)).Secili!;
            Assert.Equal(new[] { "Luca", "DijitalPlanet", "Garanti BBVA", "İş Bankası" }, panel.Sistemler.Atamalar.Select(a => a.Ad));
            Assert.Equal("0417", panel.Sistemler.Atamalar[0].FirmaKodu);
            Assert.Equal(2, panel.Sistemler.Atamalar.Count(a => a.Tur == SistemTuru.Banka));
            Assert.Equal("DijitalPlanet şifresi firma yetkilisinde", panel.Sistemler.SistemNotu);

            // ORKA kodu taşınmadı: Firma.OrkaFirmaKodu (aktarım yükünün okuduğu alan) yazıldı.
            Assert.Equal("0001", (await db.Firmalar.AsNoTracking().SingleAsync(f => f.Id == Carriere)).OrkaFirmaKodu);
            Assert.Equal("0001", panel.Siniflandirma.OrkaFirmaKodu);
            Assert.Empty(db.FirmaSistemleri.Where(f => f.FirmaKodu == "0001"));

            // Gönderilmeyen atama silinir.
            await servis.FirmaSistemleriKaydetAsync(Carriere, new FirmaSistemleriKaydetDto
            {
                Atamalar = { new() { SistemId = garanti.Id } }, OrkaFirmaKodu = "0001", MizanFormati = "Luca"
            });
            Assert.Single(await db.FirmaSistemleri.Where(f => f.FirmaId == Carriere).ToListAsync());
        }

        [Fact]
        public async Task Ayni_sistem_iki_kez_ve_gecersiz_mizan_formati_reddedilir()
        {
            using var db = Context();
            var servis = Servis(db);

            await Assert.ThrowsAsync<SistemKuralException>(() => servis.FirmaSistemleriKaydetAsync(Carriere,
                new FirmaSistemleriKaydetDto { Atamalar = { new() { SistemId = Id(db, "Luca") }, new() { SistemId = Id(db, "Luca") } } }));
            await Assert.ThrowsAsync<SistemKuralException>(() => servis.FirmaSistemleriKaydetAsync(Carriere,
                new FirmaSistemleriKaydetDto { MizanFormati = "Excel" }));
        }

        // ---- Pasife alma ----

        [Fact]
        public async Task Pasif_sistem_yeni_secimde_cikmaz_mevcut_atama_bozulmaz()
        {
            using var db = Context();
            var servis = Servis(db);
            var turmob = Id(db, "TURMOB");
            await servis.FirmaSistemleriKaydetAsync(Carriere, new FirmaSistemleriKaydetDto
            {
                Atamalar = { new() { SistemId = turmob, FirmaKodu = "T-9" } }, MizanFormati = "Luca", OrkaFirmaKodu = "0521"
            });

            await servis.GuncelleAsync(turmob, new SistemGuncelleDto { Ad = "TURMOB", Aktif = false });

            Assert.DoesNotContain(await servis.ListeAsync(SistemTuru.EFatura, pasiflerDahil: false), s => s.Id == turmob);
            Assert.Contains(await servis.ListeAsync(SistemTuru.EFatura, pasiflerDahil: true), s => s.Id == turmob && !s.Aktif);

            // Mevcut atama yerinde, pasif olarak işaretli; aynı firmanın formu onunla kaydedilebilir.
            var atama = Assert.Single((await Panel(db, Carriere)).Secili!.Sistemler.Atamalar);
            Assert.False(atama.Aktif);
            Assert.Equal("T-9", atama.FirmaKodu);
            await servis.FirmaSistemleriKaydetAsync(Carriere, new FirmaSistemleriKaydetDto
            {
                Atamalar = { new() { SistemId = turmob, FirmaKodu = "T-9" } }, MizanFormati = "Luca", OrkaFirmaKodu = "0521"
            });

            // Başka firmaya yeni atanamaz.
            await Assert.ThrowsAsync<SistemKuralException>(() => servis.FirmaSistemleriKaydetAsync(Dgr,
                new FirmaSistemleriKaydetDto { Atamalar = { new() { SistemId = turmob } } }));
        }

        // ---- Birleştirme ----

        [Fact]
        public async Task Birlestirme_atamalari_ve_is_programlarini_tasir_cift_atamayi_tekler()
        {
            using var db = Context();
            var servis = Servis(db);
            var dogru = Id(db, "DijitalPlanet");
            var yanlis = (await servis.EkleAsync(new SistemEkleDto { Ad = "Dijital Planet", Tur = SistemTuru.EFatura, Israr = true })).Id;

            // CARRIERE yalnız yanlış kayıtta; DGR ikisinde de (kodu yanlış kayıtta).
            db.FirmaSistemleri.AddRange(
                new FirmaSistemi { FirmaId = Carriere, SistemId = yanlis, FirmaKodu = "C-1", Sira = 1 },
                new FirmaSistemi { FirmaId = Dgr, SistemId = dogru, Sira = 1 },
                new FirmaSistemi { FirmaId = Dgr, SistemId = yanlis, FirmaKodu = "D-7", Sira = 2 });
            db.FirmaIsleri.Add(new FirmaIsi { FirmaId = Carriere, Baslik = "e-Fatura kontrolü", SistemId = yanlis });
            await db.SaveChangesAsync();

            var sonuc = await servis.BirlestirAsync(yanlis, dogru);

            Assert.Equal(1, sonuc.TasinanAtama);
            Assert.Equal(1, sonuc.BirlesenAtama);
            Assert.Equal(1, sonuc.TasinanIs);
            Assert.False(await db.Sistemler.AnyAsync(s => s.Id == yanlis));

            var atamalar = await db.FirmaSistemleri.AsNoTracking().ToListAsync();
            Assert.All(atamalar, a => Assert.Equal(dogru, a.SistemId));
            Assert.Equal("C-1", atamalar.Single(a => a.FirmaId == Carriere).FirmaKodu);
            Assert.Equal("D-7", Assert.Single(atamalar, a => a.FirmaId == Dgr).FirmaKodu); // kod kaybolmadı
            Assert.Equal(dogru, (await db.FirmaIsleri.AsNoTracking().SingleAsync()).SistemId);

            var liste = await servis.ListeAsync(SistemTuru.EFatura, true);
            Assert.Equal(2, liste.Single(s => s.Id == dogru).FirmaSayisi);
        }

        [Fact]
        public async Task Farkli_turler_birlestirilemez()
        {
            using var db = Context();

            await Assert.ThrowsAsync<SistemKuralException>(() => Servis(db).BirlestirAsync(Id(db, "TURMOB"), Id(db, "Luca")));
        }

        // ---- Sınıflandırma kartı ----

        [Fact]
        public async Task Siniflandirma_yeni_alanlari_yazar_sgk_orani_tek_yerden_gelir()
        {
            using var db = Context();

            await new FirmaKunyeService(db).SiniflandirmaKaydetAsync(Carriere, new FirmaSiniflandirmaKaydetDto
            {
                FirmaTipi = FirmaTipi.SermayeSirketi,
                SgkTesvikKademesi = SgkTesvikKademesi.Imalat,
                MuhtasarDonemi = MuhtasarDonemi.UcAylik
            });

            var sinif = (await Panel(db, Carriere)).Secili!.Siniflandirma;
            Assert.Equal(FirmaTipi.SermayeSirketi, sinif.FirmaTipi);
            Assert.Equal(MuhtasarDonemi.UcAylik, sinif.MuhtasarDonemi);
            Assert.Equal(16.75m, sinif.SgkIsverenOrani);
            Assert.Equal(new[] { 21.75m, 19.75m, 16.75m }, sinif.SgkKademeleri.Select(k => k.IsverenOrani));

            // Mizan formatı gönderilmedi (Prompt 9 formu): eski değer korunur.
            Assert.Equal("Luca", sinif.MizanFormati);
        }

        [Fact]
        public void Muhasebe_programindan_mizan_formati_onerisi()
        {
            Assert.Equal(new[] { "ORKA — döviz kolonlu", "ORKA — döviz kolonsuz" }, MizanFormatlari.ProgramIcin("ORKA"));
            Assert.Equal(new[] { "Luca" }, MizanFormatlari.ProgramIcin("luca"));
            Assert.Empty(MizanFormatlari.ProgramIcin("DijitalPlanet"));
        }

        [Fact]
        public async Task Mizan_formati_eksigi_kullanilan_sistemler_kartina_gider()
        {
            using var db = Context();

            var eksik = (await Panel(db, Dgr)).Secili!.Eksikler.Single(e => e.Odak == "mizanFormati");

            Assert.Equal("Kullanılan sistemler", eksik.Kart);
        }
    }
}
