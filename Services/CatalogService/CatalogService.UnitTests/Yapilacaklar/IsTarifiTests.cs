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
    /// <summary>İş tarifi (Prompt 14 · ADIM 3): iş bazında tarif, sessiz birleştirme yok.</summary>
    public class IsTarifiTests
    {
        private const int Dgr = 3;
        private const int Cubic = 4;
        private const int Aitex = 5;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Cubic, Unvan = "CUBIC LTD.", KisaAd = "CUBIC", VergiKimlikNo = "1111111111", Aktif = true },
                new Firma { Id = Aitex, Unvan = "AITEX TURKEY", KisaAd = "AITEX", VergiKimlikNo = "2222222222", Aktif = true });
            db.FirmaSicilBilgileri.AddRange(
                new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr, MukellefiyetTurleri = "0015 - KATMA DEĞER VERGİSİ" },
                new FirmaSicilBilgisi { Id = 2, FirmaId = Cubic, MukellefiyetTurleri = "0015 - KATMA DEĞER VERGİSİ" });
            db.SaveChanges();
            VergiTakvimiSeed.SeedAsync(db, new DateTime(2026, 9, 28)).GetAwaiter().GetResult();
            return db;
        }

        private static SahteSaat Saat() => new(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));

        private static IsTarifiService Servis(CatalogContext db) => new(db, Saat(), new SabitKullanici(16));

        private static FirmaIsi Is(int firmaId, string baslik, string? nasil = null, string? menu = null) => new()
        {
            FirmaId = firmaId, Baslik = baslik, Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.DonemSonu,
            NasilYapilir = nasil, MenuYolu = menu, OlusturmaZamani = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        [Theory]
        [InlineData("FATURA GİRİŞİ", "Fatura girişi")]
        [InlineData("FATURA GIRISI", "fatura girisi")]
        [InlineData("Luca'dan al, şablona yapıştır.", "lucadan al şablona  yapıştır")]
        [InlineData("1. Bordro\n2. Gönder", "1 bordro 2 gönder")]
        public void Normalize_turkce_kucultur_bosluk_ve_noktalamayi_atar(string a, string b)
            => Assert.Equal(IsTarifiKurucu.Normalize(a), IsTarifiKurucu.Normalize(b));

        [Fact]
        public void Normalize_farkli_metni_ayirt_eder()
            => Assert.NotEqual(IsTarifiKurucu.Normalize("Rapor gönder"), IsTarifiKurucu.Normalize("Rapor gönderme"));

        [Fact]
        public async Task Uretim_bos_ve_ayni_metni_baglar_farkliyi_baglamaz_ve_raporlar()
        {
            using var db = Context();
            var dolu = Is(Dgr, "Bordro", "Luca'dan bordroyu al.\nHollanda'ya gönder.", "Bordro > Raporlar");
            var ayni = Is(Cubic, "BORDRO", "lucadan bordroyu al hollandaya gönder", "bordro raporlar");
            var bos = Is(Aitex, "bordro");
            var farkli = Is(Aitex, "Bordro ", "Logo'dan al");   // aynı firmada ikinci kayıt da olabilir
            var tarifsiz = Is(Dgr, "Stok sayımı");
            db.FirmaIsleri.AddRange(dolu, ayni, bos, farkli, tarifsiz);
            await db.SaveChangesAsync();

            var rapor = await Servis(db).UretAsync(null);

            Assert.Equal(2, rapor.GrupSayisi);           // bordro, stok sayımı
            Assert.Equal(1, rapor.UretilenTarif);        // stok sayımında metin yok → tarif yok
            Assert.Equal(3, rapor.BaglananKayit);
            var tarif = await db.IsTarifleri.SingleAsync();
            Assert.Equal("Luca'dan bordroyu al.\nHollanda'ya gönder.", tarif.NasilYapilir);   // EN DOLU kayıttan
            Assert.Equal(tarif.Id, (await db.FirmaIsleri.FindAsync(dolu.Id))!.IsTarifiId);
            Assert.Equal(tarif.Id, (await db.FirmaIsleri.FindAsync(ayni.Id))!.IsTarifiId);
            Assert.Equal(tarif.Id, (await db.FirmaIsleri.FindAsync(bos.Id))!.IsTarifiId);
            Assert.Null((await db.FirmaIsleri.FindAsync(farkli.Id))!.IsTarifiId);           // sessiz birleştirme yok
            Assert.Null((await db.FirmaIsleri.FindAsync(tarifsiz.Id))!.IsTarifiId);

            var fark = Assert.Single(rapor.Baglanmayanlar);
            Assert.Equal(1, fark.KayitSayisi);
            Assert.Equal(1, fark.FarkliMetinSayisi);
            Assert.Equal(new[] { "AITEX" }, fark.Firmalar);

            // Firma metinleri SİLİNMEDİ.
            Assert.Equal("Logo'dan al", (await db.FirmaIsleri.FindAsync(farkli.Id))!.NasilYapilir);
            Assert.Equal("lucadan bordroyu al hollandaya gönder", (await db.FirmaIsleri.FindAsync(ayni.Id))!.NasilYapilir);
        }

        [Fact]
        public async Task Liste_farkli_tarif_ve_tarif_yok_isaretlerini_tasir()
        {
            using var db = Context();
            db.FirmaIsleri.AddRange(Is(Dgr, "Bordro", "A adımı"), Is(Cubic, "Bordro", "B adımı"), Is(Dgr, "Stok sayımı"));
            await db.SaveChangesAsync();
            await Servis(db).UretAsync(null);

            var liste = await Servis(db).ListeAsync();

            var bordro = liste.Single(l => l.Anahtar == "o-bordro-2");
            Assert.NotNull(bordro.TarifId);
            Assert.Equal(1, bordro.FarkliSayisi);
            Assert.Equal(2, bordro.FirmaSayisi);
            Assert.Equal(new[] { Dgr, Cubic }, bordro.FirmaIdleri.OrderBy(i => i));   // Prompt 15: firma seçimiyle süzme
            Assert.Null(liste.Single(l => l.Anahtar == IsTarifiKurucu.OzelAnahtar("Stok sayımı", IsTekrari.Aylik)).TarifId);
        }

        [Fact]
        public async Task Bu_tarifi_kullan_baglar_metni_silmez_ve_bu_firmada_farkli_gorunur()
        {
            using var db = Context();
            var a = Is(Dgr, "Bordro", "A adımı");
            var b = Is(Cubic, "Bordro", "B adımı");
            db.FirmaIsleri.AddRange(a, b);
            await db.SaveChangesAsync();
            var s = Servis(db);
            await s.UretAsync(null);
            var tarifId = (await db.FirmaIsleri.FindAsync(a.Id))!.IsTarifiId!.Value;

            var once = await s.DetayAsync("o-bordro-2");
            Assert.Equal(TarifBaglantisi.Farkli, once.Firmalar.Single(f => f.FirmaId == Cubic).Baglanti);

            await s.BaglaAsync(tarifId, b.Id);

            var sonra = await s.DetayAsync("o-bordro-2");
            var cubic = sonra.Firmalar.Single(f => f.FirmaId == Cubic);
            Assert.Equal(TarifBaglantisi.Bagli, cubic.Baglanti);
            Assert.True(cubic.FirmayaOzel);                       // "Bu firmada farklı"
            Assert.Equal("B adımı", cubic.NasilYapilir);
            Assert.False(sonra.Firmalar.Single(f => f.FirmaId == Dgr).FirmayaOzel);
        }

        [Fact]
        public async Task Baska_isin_tarifine_baglanamaz()
        {
            using var db = Context();
            var bordro = Is(Dgr, "Bordro", "A");
            var rapor = Is(Cubic, "Rapor gönder", "B");
            db.FirmaIsleri.AddRange(bordro, rapor);
            await db.SaveChangesAsync();
            var s = Servis(db);
            await s.UretAsync(null);
            var bordroTarifi = (await db.FirmaIsleri.FindAsync(bordro.Id))!.IsTarifiId!.Value;

            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => s.BaglaAsync(bordroTarifi, rapor.Id));
        }

        [Fact]
        public async Task Yasal_tarif_ortaktir_satiri_olmayan_firma_ortak_kullanir()
        {
            using var db = Context();
            db.FirmaIsleri.Add(new FirmaIsi
            {
                FirmaId = Dgr, Baslik = "KDV", YasalMukellefiyetKodu = "0015", Tekrar = IsTekrari.Aylik,
                NasilYapilir = "e-Beyanname'den gönder", OlusturmaZamani = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var s = Servis(db);
            await s.UretAsync(null);

            var detay = await s.DetayAsync(IsTarifiKurucu.YasalAnahtar("0015", IsTekrari.Aylik));

            Assert.NotNull(detay.Tarif);
            Assert.Equal(TarifBaglantisi.Bagli, detay.Firmalar.Single(f => f.FirmaId == Dgr).Baglanti);
            Assert.Equal(TarifBaglantisi.Ortak, detay.Firmalar.Single(f => f.FirmaId == Cubic).Baglanti);
            Assert.DoesNotContain(detay.Firmalar, f => f.FirmaId == Aitex);   // AITEX'te KDV yok
        }

        [Fact]
        public async Task Ayni_baslikli_aylik_ve_yillik_is_ayri_tariftir()
        {
            using var db = Context();
            var aylik = Is(Dgr, "Defter tasdiki", "Aylık adımlar");
            var yillik = Is(Cubic, "Defter tasdiki", "Yıllık adımlar");
            yillik.Tekrar = IsTekrari.Yillik;
            db.FirmaIsleri.AddRange(aylik, yillik);
            await db.SaveChangesAsync();

            var rapor = await Servis(db).UretAsync(null);

            Assert.Equal(2, rapor.UretilenTarif);
            Assert.Empty(rapor.Baglanmayanlar);                 // farklı metin değil, farklı iş
            Assert.NotEqual(IsTarifiKurucu.Anahtar(aylik), IsTarifiKurucu.Anahtar(yillik));
        }

        [Fact]
        public async Task Bos_metinli_baglanmamis_kayit_baglanabilir_sayilir_ve_toplu_baglanir()
        {
            using var db = Context();
            db.FirmaIsleri.AddRange(Is(Dgr, "Bordro", "Uzun tarif metni"), Is(Cubic, "Bordro", "B adımı"));
            await db.SaveChangesAsync();
            var s = Servis(db);
            await s.UretAsync(null);

            // Seed sonrası eklenen boş metinli işler kendiliğinden BAĞLANMAZ.
            var yeni1 = Is(Aitex, "BORDRO");
            var yeni2 = Is(Cubic, "bordro");
            db.FirmaIsleri.AddRange(yeni1, yeni2);
            await db.SaveChangesAsync();

            var oge = (await s.ListeAsync()).Single(l => l.Anahtar == "o-bordro-2");
            Assert.Equal(2, oge.BaglanabilirSayisi);
            Assert.Equal(1, oge.FarkliSayisi);
            Assert.Null((await db.FirmaIsleri.FindAsync(yeni1.Id))!.IsTarifiId);

            var farkli = await db.FirmaIsleri.SingleAsync(i => i.NasilYapilir == "B adımı");
            var baglanan = await s.HepsiniBaglaAsync(oge.TarifId!.Value, new[] { yeni1.Id, yeni2.Id, farkli.Id });

            Assert.Equal(2, baglanan);                           // farklı metin toplu bağlanmaz
            Assert.Null((await db.FirmaIsleri.FindAsync(farkli.Id))!.IsTarifiId);
            Assert.Equal(0, (await s.ListeAsync()).Single(l => l.Anahtar == "o-bordro-2").BaglanabilirSayisi);
        }

        [Fact]
        public async Task Prosedur_paneli_tarifi_ve_firmaya_ozel_metni_okur()
        {
            using var db = Context();
            var a = Is(Dgr, "Bordro", "Tarif adımı 1\nTarif adımı 2", "Bordro > Rapor");
            var b = Is(Cubic, "Bordro", "Cubic'e özel");
            db.FirmaIsleri.AddRange(a, b);
            await db.SaveChangesAsync();
            var s = Servis(db);
            await s.UretAsync(null);
            await s.BaglaAsync((await db.FirmaIsleri.FindAsync(a.Id))!.IsTarifiId!.Value, b.Id);
            var isler = new YapilacaklarService(db, Saat(), new SabitKullanici(16),
                new Api.Features.Anasayfa.Services.FirmaOlayYazici(db, new SabitKullanici(16),
                    NullLogger<Api.Features.Anasayfa.Services.FirmaOlayYazici>.Instance));

            var p = await isler.ProsedurAsync(Cubic, IsKaynagi.Ozel, b.Id, false);

            Assert.NotNull(p.TarifId);
            Assert.Equal(new[] { "Tarif adımı 1", "Tarif adımı 2" }, p.TarifAdimlar);
            Assert.Equal("Bordro > Rapor", p.TarifMenuYolu);
            Assert.True(p.FirmayaOzel);
            Assert.Equal(new[] { "Cubic'e özel" }, p.Adimlar);    // firmanın metni silinmedi
            Assert.Equal("o-bordro-2", p.TarifAnahtari);
        }
        // ---- Prompt 15: "+ Tarif yaz" — formdan sıfırdan tarif ----

        private static IsTarifiService ServisIslerle(CatalogContext db)
            => new(db, Saat(), new SabitKullanici(16),
                   new YapilacaklarService(db, Saat(), new SabitKullanici(16),
                       new Api.Features.Anasayfa.Services.FirmaOlayYazici(db, new SabitKullanici(16),
                           NullLogger<Api.Features.Anasayfa.Services.FirmaOlayYazici>.Instance)));

        [Fact]
        public async Task Hicbir_firmada_metin_yokken_formdan_tarif_yazilir_ve_bos_kayitlar_baglanir()
        {
            using var db = Context();
            var a = Is(Dgr, "Stok sayımı");
            var b = Is(Cubic, "STOK SAYIMI");
            db.FirmaIsleri.AddRange(a, b);
            await db.SaveChangesAsync();
            var s = Servis(db);
            var anahtar = IsTarifiKurucu.OzelAnahtar("Stok sayımı", IsTekrari.Aylik);

            // Eski yol (gövdesiz) hâlâ reddeder: metin yok.
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => s.UretAsync(anahtar));
            // Boş form da reddedilir.
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => s.UretAsync(anahtar, default, new IsTarifiKaydetDto { Ad = "Stok" }));

            var rapor = await s.UretAsync(anahtar, default, new IsTarifiKaydetDto { NasilYapilir = "Depoyu say\nFarkı yaz" });

            Assert.Equal(2, rapor.BaglananKayit);
            var detay = await s.DetayAsync(anahtar);
            Assert.Equal(new[] { "Depoyu say", "Farkı yaz" }, detay.Tarif!.Adimlar);
            Assert.Equal("Stok sayımı", detay.Tarif.Ad);                  // ad boşsa işin başlığı
            Assert.All(detay.Firmalar, f => Assert.Equal(TarifBaglantisi.Bagli, f.Baglanti));
        }

        [Fact]
        public async Task Formdan_tarifte_de_farkli_metin_baglanmaz()
        {
            using var db = Context();
            db.FirmaIsleri.AddRange(Is(Dgr, "Bordro"), Is(Cubic, "Bordro", "Logo'dan al"));
            await db.SaveChangesAsync();

            var rapor = await Servis(db).UretAsync("o-bordro-2", default, new IsTarifiKaydetDto { NasilYapilir = "Luca'dan al" });

            Assert.Equal(1, rapor.BaglananKayit);
            Assert.Equal(1, Assert.Single(rapor.Baglanmayanlar).KayitSayisi);
            Assert.Equal("Logo'dan al", (await db.FirmaIsleri.SingleAsync(i => i.FirmaId == Cubic)).NasilYapilir);
        }

        [Fact]
        public async Task Yasal_iste_satir_yoksa_mevcut_akisla_acilir_ve_tarif_ortak_olur()
        {
            using var db = Context();
            Assert.Equal(0, await db.FirmaIsleri.CountAsync());
            var s = ServisIslerle(db);
            var anahtar = IsTarifiKurucu.YasalAnahtar("0015", IsTekrari.Aylik);

            await s.UretAsync(anahtar, default, new IsTarifiKaydetDto { NasilYapilir = "e-Beyanname'den gönder" }, firmaId: Cubic);

            var satir = await db.FirmaIsleri.SingleAsync();                 // tek satır açıldı — seçili firmada
            Assert.Equal(Cubic, satir.FirmaId);
            Assert.Equal("0015", satir.YasalMukellefiyetKodu);
            Assert.NotNull(satir.IsTarifiId);

            var detay = await s.DetayAsync(anahtar);
            Assert.Equal(TarifBaglantisi.Bagli, detay.Firmalar.Single(f => f.FirmaId == Cubic).Baglanti);
            Assert.Equal(TarifBaglantisi.Ortak, detay.Firmalar.Single(f => f.FirmaId == Dgr).Baglanti);   // ortak tarif
        }

        [Fact]
        public async Task Seed_tek_seferliktir_tarif_varken_dokunmaz()
        {
            using var db = Context();
            db.FirmaIsleri.Add(Is(Dgr, "Bordro", "A"));
            await db.SaveChangesAsync();

            await IsTarifiSeed.SeedVeLoglaAsync(db, NullLogger.Instance);
            db.FirmaIsleri.Add(Is(Cubic, "Rapor", "B"));
            await db.SaveChangesAsync();
            await IsTarifiSeed.SeedVeLoglaAsync(db, NullLogger.Instance);

            Assert.Equal(1, await db.IsTarifleri.CountAsync());
        }

        [Fact]
        public async Task Tarif_olustur_tek_grup_icin_ve_tarifi_varsa_reddeder()
        {
            using var db = Context();
            db.FirmaIsleri.AddRange(Is(Dgr, "Bordro", "A"), Is(Cubic, "Rapor", "B"));
            await db.SaveChangesAsync();
            var s = Servis(db);

            var rapor = await s.UretAsync("o-rapor-2");

            Assert.Equal(1, rapor.UretilenTarif);
            Assert.Null((await db.FirmaIsleri.SingleAsync(i => i.Baslik == "Bordro")).IsTarifiId);
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() => s.UretAsync("o-rapor-2"));
        }
    }
}
