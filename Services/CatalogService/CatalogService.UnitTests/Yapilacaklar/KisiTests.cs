using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Yapilacaklar;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogService.UnitTests.Yapilacaklar
{
    /// <summary>Kişiler ve mailler (Prompt 14 · ADIM 4). Bugün 02.10.2026.</summary>
    public class KisiTests
    {
        private const int Dgr = 3;
        private const int Cubic = 4;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Cubic, Unvan = "CUBIC LTD.", KisaAd = "CUBIC", VergiKimlikNo = "1111111111", Aktif = true });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr, MukellefiyetTurleri = "0015 - KATMA DEĞER VERGİSİ" });
            db.SaveChanges();
            VergiTakvimiSeed.SeedAsync(db, new DateTime(2026, 9, 28)).GetAwaiter().GetResult();
            return db;
        }

        private static SahteSaat Saat() => new(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));

        private static YapilacaklarService Isler(CatalogContext db)
            => new(db, Saat(), new SabitKullanici(16),
                   new FirmaOlayYazici(db, new SabitKullanici(16), NullLogger<FirmaOlayYazici>.Instance));

        private static KisiService Kisiler(CatalogContext db) => new(db, Saat());

        private static FirmaIsiKaydetDto Bordro(int gun, params FirmaIsiAlicisiDto[] alicilar) => new()
        {
            Baslik = "Bordro", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu, AyinGunu = gun,
            Alicilar = alicilar.ToList()
        };

        private static FirmaIsiAlicisiDto Alici(string ad, string? eposta, AliciTipi tip = AliciTipi.Kime)
            => new() { AdSoyad = ad, Eposta = eposta, AliciTipi = tip };

        // ---- Eşleştirme kuralı ----

        [Fact]
        public void Ayni_eposta_tek_kisi_buyuk_kucuk_harf_farki_yok()
        {
            var liste = new List<Kisi>();
            var (a, _) = KisiEslestirici.Esle(liste, Dgr, "J. van Dijk", "J.vanDijk@firma.nl", null);
            var (b, sonuc) = KisiEslestirici.Esle(liste, Dgr, "Jan van Dijk", " j.vandijk@FIRMA.nl ", null);

            Assert.Same(a, b);
            Assert.Equal(KisiEslestirici.Sonuc.EpostaIleBulundu, sonuc);
            Assert.Single(liste);
        }

        [Fact]
        public void Epostasiz_ada_gore_eslesir_belirsizse_secilmez()
        {
            var liste = new List<Kisi>
            {
                new() { Id = 1, FirmaId = Dgr, Ad = "R. de Vries", Eposta = "r1@x.nl" },
                new() { Id = 2, FirmaId = Dgr, Ad = "R. DE VRIES", Eposta = "r2@x.nl" },
                new() { Id = 3, FirmaId = Dgr, Ad = "Ayşe Işık", Eposta = "ayse@x.com" }
            };

            var (ayse, s1) = KisiEslestirici.Esle(liste, Dgr, "AYŞE IŞIK", null, null);
            var (yeni, s2) = KisiEslestirici.Esle(liste, Dgr, "r. de vries", null, null);

            Assert.Equal(3, ayse.Id);
            Assert.Equal(KisiEslestirici.Sonuc.AdIleBulundu, s1);
            Assert.Equal(KisiEslestirici.Sonuc.YeniKisiBelirsiz, s2);   // iki aday: seçilmez
            Assert.Equal(0, yeni.Id);
            Assert.Null(yeni.Eposta);

            // Aynı belirsiz ad tekrar yazılırsa açılan e-postasız kişi bulunur — her kayıtta yeni kişi açılmaz.
            var (tekrar, s3) = KisiEslestirici.Esle(liste, Dgr, "R. de Vries", null, null);
            Assert.Same(yeni, tekrar);
            Assert.Equal(KisiEslestirici.Sonuc.AdIleBulundu, s3);
        }

        [Fact]
        public void Farkli_firmada_ayni_eposta_ayri_kisidir()
        {
            var dgr = new List<Kisi> { new() { Id = 1, FirmaId = Dgr, Ad = "J", Eposta = "j@x.nl" } };
            var cubic = new List<Kisi>();
            var (k, sonuc) = KisiEslestirici.Esle(cubic, Cubic, "J", "j@x.nl", null);
            Assert.Equal(KisiEslestirici.Sonuc.YeniKisi, sonuc);
            Assert.Equal(Cubic, k.FirmaId);
        }

        [Fact]
        public void Supheliler_ayni_ve_benzer_adi_raporlar_birlestirmez()
        {
            var kisiler = new List<Kisi>
            {
                new() { Id = 1, FirmaId = Dgr, Ad = "R. de Vries", Eposta = "r1@x.nl" },
                new() { Id = 2, FirmaId = Dgr, Ad = "R. de Vries", Eposta = "r2@x.nl" },
                new() { Id = 3, FirmaId = Dgr, Ad = "Mehmet Yılmaz" },
                new() { Id = 4, FirmaId = Dgr, Ad = "Mehmet Yilmazz" },
                new() { Id = 5, FirmaId = Cubic, Ad = "R. de Vries" }
            };

            var s = KisiEslestirici.Supheliler(kisiler);

            Assert.Contains(s, x => x.Tur == KisiEslestirici.SupheTuru.AyniAd && x.Kisiler.Select(k => k.Id).SequenceEqual(new[] { 1, 2 }));
            Assert.Contains(s, x => x.Tur == KisiEslestirici.SupheTuru.BenzerAd && x.Kisiler.Select(k => k.Id).SequenceEqual(new[] { 3, 4 }));
            Assert.DoesNotContain(s, x => x.Kisiler.Any(k => k.Id == 5));      // başka firma
            Assert.Equal(5, kisiler.Count);                                     // hiçbir şey silinmedi
        }

        // ---- Yazma yolu ve taşıma ----

        [Fact]
        public async Task Is_yazilirken_alicilar_kisiye_baglanir_ayni_eposta_tek_kisi()
        {
            using var db = Context();
            var isler = Isler(db);
            await isler.IsEkleAsync(Dgr, Bordro(25, Alici("J. van Dijk", "j@x.nl"), Alici("R. de Vries", null, AliciTipi.Bilgi)));
            var rapor = Bordro(1, Alici("Jan van Dijk", "J@X.NL"));
            rapor.Baslik = "Rapor";
            await isler.IsEkleAsync(Dgr, rapor);

            var kisiler = await db.Kisiler.ToListAsync();
            Assert.Equal(2, kisiler.Count);
            Assert.All(await db.FirmaIsiAlicilari.ToListAsync(), a => Assert.NotNull(a.KisiId));
            var jan = kisiler.Single(k => k.Eposta == "j@x.nl");
            Assert.Equal(2, await db.FirmaIsiAlicilari.CountAsync(a => a.KisiId == jan.Id));
            Assert.All(await db.FirmaIsiAlicilari.Where(a => a.KisiId == jan.Id).ToListAsync(),
                       a => Assert.Equal("J. van Dijk", a.AdSoyad));             // kopya kişiden
        }

        [Fact]
        public async Task Kisi_duzenlenince_bagli_islerin_alicisi_birden_duzelir()
        {
            using var db = Context();
            var isler = Isler(db);
            await isler.IsEkleAsync(Dgr, Bordro(25, Alici("J. van Dijk", "j@x.nl")));
            var rapor = Bordro(1, Alici("J. van Dijk", "j@x.nl"));
            rapor.Baslik = "Rapor";
            await isler.IsEkleAsync(Dgr, rapor);
            var kisi = await db.Kisiler.SingleAsync();

            await Kisiler(db).GuncelleAsync(kisi.Id, new KisiKaydetDto { FirmaId = Dgr, Ad = "J. van Dijk", Eposta = "yeni@x.nl" });

            Assert.All(await db.FirmaIsiAlicilari.ToListAsync(), a => Assert.Equal("yeni@x.nl", a.Eposta));
        }

        [Fact]
        public async Task Ayni_firmada_ayni_eposta_ikinci_kisiye_yazilamaz()
        {
            using var db = Context();
            var s = Kisiler(db);
            await s.EkleAsync(new KisiKaydetDto { FirmaId = Dgr, Ad = "A", Eposta = "a@x.nl" });

            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => s.EkleAsync(new KisiKaydetDto { FirmaId = Dgr, Ad = "B", Eposta = "A@x.nl" }));
            await s.EkleAsync(new KisiKaydetDto { FirmaId = Cubic, Ad = "A", Eposta = "a@x.nl" });   // başka firma serbest
        }

        [Fact]
        public async Task Tasima_eski_alicilardan_kisi_uretir_ve_raporlar()
        {
            using var db = Context();
            var b1 = new FirmaIsi { FirmaId = Dgr, Baslik = "Bordro", Tekrar = IsTekrari.Aylik, OlusturmaZamani = DateTime.UtcNow };
            b1.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "J. van Dijk", Eposta = "j@x.nl", Sira = 1 });
            b1.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "R. de Vries", Sira = 2 });
            var b2 = new FirmaIsi { FirmaId = Dgr, Baslik = "Rapor", Tekrar = IsTekrari.Aylik, OlusturmaZamani = DateTime.UtcNow };
            b2.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "Jan van Dijk", Eposta = "J@x.nl", Sira = 1 });
            b2.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "r. de vries", Sira = 2 });
            var c1 = new FirmaIsi { FirmaId = Cubic, Baslik = "Bordro", Tekrar = IsTekrari.Aylik, OlusturmaZamani = DateTime.UtcNow };
            c1.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "J. van Dijk", Eposta = "j@x.nl", Sira = 1 });
            db.FirmaIsleri.AddRange(b1, b2, c1);
            await db.SaveChangesAsync();

            var r = await KisiService.TasiAsync(db);

            Assert.Equal(5, r.AliciKaydi);
            Assert.Equal(3, r.YeniKisi);                     // DGR: J + R, CUBIC: J
            Assert.Equal(1, r.EpostaIleBirlesen);
            Assert.Equal(1, r.AdIleBirlesen);
            Assert.Single(r.FarkliAdYazimlari);              // J. van Dijk / Jan van Dijk
            Assert.Equal(3, await db.Kisiler.CountAsync());
            Assert.Equal(0, await db.FirmaIsiAlicilari.CountAsync(a => a.KisiId == null));

            var ikinci = await KisiService.TasiAsync(db);    // tekrar güvenli
            Assert.Equal(0, ikinci.AliciKaydi);
            Assert.Equal(3, await db.Kisiler.CountAsync());
        }

        // ---- İş dialogu önizlemesi (Prompt 14B): davranış aynı, görünür ----

        [Fact]
        public async Task Onizleme_bosaltilan_epostanin_kisiden_gelecegini_soyler_ve_kaydetmez()
        {
            using var db = Context();
            await Isler(db).IsEkleAsync(Dgr, Bordro(25, Alici("J. van Dijk", "j@x.nl")));
            var kisi = await db.Kisiler.SingleAsync();

            var o = await Kisiler(db).OnizleAsync(new AliciOnizlemeIstegiDto
            {
                FirmaId = Dgr, Alicilar = { Alici("J. van Dijk", null), Alici("Yeni Kişi", null) }
            });

            Assert.True(o[0].Kayitli);
            Assert.Equal(kisi.Id, o[0].KisiId);
            Assert.Equal("j@x.nl", o[0].KayitliEposta);
            Assert.True(o[0].EpostaKisidenGelir);
            Assert.False(o[0].AdFarkli);
            Assert.False(o[1].Kayitli);                           // yeni kişi açılacak, uyarı yok
            Assert.Equal(1, await db.Kisiler.CountAsync());       // önizleme kaydetmez

            // Davranış değişmedi: kaydedilince e-posta kişiden gelir.
            var isi = await db.FirmaIsleri.SingleAsync();
            await Isler(db).IsGuncelleAsync(Dgr, isi.Id, Bordro(25, Alici("J. van Dijk", null)));
            Assert.Equal("j@x.nl", (await db.FirmaIsiAlicilari.SingleAsync()).Eposta);
        }

        [Fact]
        public async Task Onizleme_ayni_epostaya_farkli_adi_bildirir()
        {
            using var db = Context();
            await Isler(db).IsEkleAsync(Dgr, Bordro(25, Alici("J. van Dijk", "j@x.nl")));

            var o = (await Kisiler(db).OnizleAsync(new AliciOnizlemeIstegiDto
            {
                FirmaId = Dgr, Alicilar = { Alici("Jan van Dijk", "J@X.nl") }
            })).Single();

            Assert.True(o.Kayitli);
            Assert.True(o.AdFarkli);
            Assert.Equal("J. van Dijk", o.KayitliAd);
            Assert.False(o.EpostaKisidenGelir);
        }

        // ---- Sheet ----

        [Fact]
        public async Task Pano_firma_secimiyle_yalniz_o_firmayi_gosterir()
        {
            using var db = Context();
            await Isler(db).IsEkleAsync(Dgr, Bordro(25, Alici("J. van Dijk", "j@x.nl")));
            await Isler(db).IsEkleAsync(Cubic, Bordro(25, Alici("Ayşe", "a@c.com")));

            var pano = await Kisiler(db).PanoAsync(2026, 10, firmaId: Cubic);

            Assert.All(pano.Satirlar, s => Assert.Equal(Cubic, s.FirmaId));
            Assert.Equal(new[] { "Ayşe" }, pano.Takvim.Single().Isler.Single().Kime);   // takvim de süzülür
        }

        [Fact]
        public async Task Alicisi_olmayan_is_eksik_satir_olarak_gorunur()
        {
            using var db = Context();
            await Isler(db).IsEkleAsync(Dgr, Bordro(25));

            var pano = await Kisiler(db).PanoAsync(2026, 10);

            var satir = Assert.Single(pano.Satirlar);
            Assert.True(satir.Eksik);
            Assert.Equal("Bordro", satir.IsBaslik);
            Assert.Null(satir.KisiAdi);
            Assert.Contains(pano.AlicisizIsler, a => a.Baslik == "Bordro" && a.SonGun == new DateTime(2026, 10, 25));
        }

        [Fact]
        public async Task Mail_takvimi_turetilir_ve_isin_gunu_degisince_kayar()
        {
            using var db = Context();
            var isler = Isler(db);
            var eklenen = await isler.IsEkleAsync(Dgr, Bordro(25, Alici("J. van Dijk", "j@x.nl"), Alici("R. de Vries", null, AliciTipi.Bilgi)));
            await isler.IsEkleAsync(Cubic, Bordro(25, Alici("Ayşe", "a@c.com")));

            var once = await Kisiler(db).PanoAsync(2026, 10);
            var gun = Assert.Single(once.Takvim);
            Assert.Equal(new DateTime(2026, 10, 25), gun.Tarih);
            var bordro = Assert.Single(gun.Isler);
            Assert.Equal(2, bordro.FirmaSayisi);                  // aynı iş, iki firma, tek satır
            Assert.Equal(new[] { "Ayşe", "J. van Dijk" }, bordro.Kime);
            Assert.Equal(new[] { "R. de Vries" }, bordro.Bilgi);

            var guncel = Bordro(10, Alici("J. van Dijk", "j@x.nl"), Alici("R. de Vries", null, AliciTipi.Bilgi));
            await isler.IsGuncelleAsync(Dgr, eklenen.Id, guncel);

            var sonra = await Kisiler(db).PanoAsync(2026, 10);
            Assert.Equal(new[] { new DateTime(2026, 10, 10), new DateTime(2026, 10, 25) }, sonra.Takvim.Select(t => t.Tarih));
            Assert.Equal(1, sonra.Takvim[1].Isler.Single().FirmaSayisi);
        }

        [Fact]
        public async Task Birlestir_alicilari_hedefe_tasir_ve_bilgiyi_kaybetmez()
        {
            using var db = Context();
            var s = Kisiler(db);
            var a = await s.EkleAsync(new KisiKaydetDto { FirmaId = Dgr, Ad = "R. de Vries", Eposta = "r1@x.nl" });
            var b = await s.EkleAsync(new KisiKaydetDto { FirmaId = Dgr, Ad = "R. de Vries", Eposta = "r2@x.nl" });
            var isi = new FirmaIsi { FirmaId = Dgr, Baslik = "Bordro", Tekrar = IsTekrari.Aylik, OlusturmaZamani = DateTime.UtcNow };
            isi.Alicilar.Add(new FirmaIsiAlicisi { AdSoyad = "R. de Vries", Eposta = "r2@x.nl", KisiId = b.Id, Sira = 1 });
            db.FirmaIsleri.Add(isi);
            await db.SaveChangesAsync();

            await s.BirlestirAsync(a.Id, b.Id);

            var kisi = await db.Kisiler.SingleAsync();
            Assert.Equal(a.Id, kisi.Id);
            Assert.Contains("r2@x.nl", kisi.Notu);
            var alici = await db.FirmaIsiAlicilari.SingleAsync();
            Assert.Equal(a.Id, alici.KisiId);
            Assert.Equal("r1@x.nl", alici.Eposta);
        }

        [Fact]
        public async Task Seed_kisileri_loglar_ve_sifre_alani_yoktur()
        {
            using var db = Context();
            await KisiSeed.SeedVeLoglaAsync(db, NullLogger.Instance);   // alıcı yok: sessiz geçer

            var alanlar = typeof(Kisi).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
            Assert.DoesNotContain(alanlar, a => a.Contains("sifre") || a.Contains("parola") || a.Contains("password")
                                                || a.Contains("kullaniciadi") || a.Contains("erisim"));
        }
    }
}
