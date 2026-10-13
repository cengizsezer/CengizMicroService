using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaPortfoyu.Domain;
using CatalogService.Api.Features.FirmaPortfoyu.Dtos;
using CatalogService.Api.Features.FirmaPortfoyu.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.FirmaPortfoyu
{
    /// <summary>
    /// Firma Portföyü Aşama 1: aylık mizan yükleme. Sunucu satırları kendisi doğrular ve değerlendirir
    /// (PortfoyHesaplayici); önizleme ve kayıt aynı sonucu verir. Yalnız aktif firmalar. Sabit tarih 13.10.2026.
    /// </summary>
    public class FirmaPortfoyuTests
    {
        private const int Lime = 5, Dgr = 3, Pkf = 9;

        private static readonly DateTimeOffset Simdi = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Lime, Unvan = "LIME LTD.", KisaAd = "LIME", VergiKimlikNo = "2222222222", Aktif = true, SorumluKullaniciAdi = "Cengiz" },
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Pkf, Unvan = "PKF ADAY", KisaAd = "PKF ADAY", VergiKimlikNo = "0070511435", Aktif = false });
            db.SaveChanges();
            return db;
        }

        private static FirmaPortfoyuService Servis(CatalogContext db) => new(db, new SahteSaat(Simdi), new SabitKullanici(16));

        private static AylikMizanSatiriDto S(string kod, decimal borc, decimal alacak, string? ad = null)
            => new() { HesapKodu = kod, HesapAdi = ad ?? $"Hesap {kod}", BorcBakiye = borc, AlacakBakiye = alacak };

        /// <summary>Dengeli 7/B mizan: kümülatif satış <paramref name="satis"/>, iade 10, SMM 400, gider 150.</summary>
        private static List<AylikMizanSatiriDto> Dengeli(decimal satis = 1000m) => new()
        {
            S("102", 500m + satis - 1000m, 0m),
            S("120", 300m, 0m),
            S("320", 0m, 260m),
            S("500", 0m, 100m),
            S("600", 0m, satis),
            S("610", 10m, 0m),
            S("621", 400m, 0m),
            S("632", 150m, 0m)
        };

        private static AylikMizanKaydetDto Istek(int firma, int yil, int ay, List<AylikMizanSatiriDto> satirlar, bool uzerineYaz = false)
            => new() { FirmaId = firma, Yil = yil, Ay = ay, DosyaAdi = $"mizan-{ay}.xlsx", Satirlar = satirlar, UzerineYaz = uzerineYaz };

        // ---- hesap kuralları (saf) ----

        private static IReadOnlyDictionary<string, decimal> B(params (string Kod, decimal Bakiye)[] x)
            => x.ToDictionary(k => k.Kod, k => k.Bakiye);

        [Fact]
        public void Net_satis_SMM_ve_7B_giderleri()
        {
            var b = B(("600", -1000m), ("601", -200m), ("610", 30m), ("611", 20m), ("620", 100m), ("621", 300m),
                      ("630", 40m), ("631", 50m), ("632", 60m));

            Assert.Equal(1150m, PortfoyHesaplayici.NetSatis(b));
            Assert.Equal(400m, PortfoyHesaplayici.Smm(b));
            Assert.Equal(GiderYontemi.YediB, PortfoyHesaplayici.GiderYontemiBul(b));
            Assert.Equal(150m, PortfoyHesaplayici.FaaliyetGiderleri(b));
        }

        [Fact]
        public void Yedi_A_giderleri_750_760_770_ve_622_bossa_740()
        {
            var b = B(("600", -1000m), ("740", 250m), ("750", 10m), ("760", 20m), ("770", 30m), ("751", -10m), ("771", -30m));

            Assert.Equal(GiderYontemi.YediA, PortfoyHesaplayici.GiderYontemiBul(b));
            Assert.Equal(60m, PortfoyHesaplayici.FaaliyetGiderleri(b));   // 751/761/771 dahil değil
            Assert.Equal(250m, PortfoyHesaplayici.Smm(b));                 // 622 boş → 740
        }

        [Fact]
        public void Dolu_622_varken_740_alinmaz()
        {
            var b = B(("622", 200m), ("740", 250m));
            Assert.Equal(200m, PortfoyHesaplayici.Smm(b));
        }

        [Fact]
        public void Iki_gider_yontemi_birden_doluysa_cift_sayilmaz_ve_uyarir()
        {
            var b = B(("600", -1000m), ("632", 150m), ("770", 150m));

            Assert.Equal(GiderYontemi.Ikisi, PortfoyHesaplayici.GiderYontemiBul(b));
            Assert.Equal(150m, PortfoyHesaplayici.FaaliyetGiderleri(b));
            Assert.Contains(PortfoyHesaplayici.IkiGiderYontemiUyarisi, PortfoyHesaplayici.Uyarilar(5, b, null, 0m));
        }

        [Fact]
        public void Kapanis_sonrasi_Aralik_tespit_edilir_baska_ayda_edilmez()
        {
            var kapanmis = B(("102", 900m), ("590", -400m), ("500", -500m));

            Assert.True(PortfoyHesaplayici.KapanisSonrasi(12, kapanmis));
            Assert.False(PortfoyHesaplayici.KapanisSonrasi(11, kapanmis));
            Assert.False(PortfoyHesaplayici.KapanisSonrasi(12, B(("600", -1000m), ("690", -400m))));

            var uyarilar = PortfoyHesaplayici.Uyarilar(12, kapanmis, null, 0m);
            Assert.Contains(PortfoyHesaplayici.KapanisSonrasiUyarisi, uyarilar);
            Assert.DoesNotContain(PortfoyHesaplayici.GelirTablosuYokUyarisi, uyarilar);
        }

        [Fact]
        public void Gelir_onceki_aydan_dusukse_uyarir_600_yoksa_601()
        {
            Assert.True(PortfoyHesaplayici.GelirDustu(B(("600", -900m)), B(("600", -1000m))));
            Assert.False(PortfoyHesaplayici.GelirDustu(B(("600", -1100m)), B(("600", -1000m))));
            Assert.True(PortfoyHesaplayici.GelirDustu(B(("601", -50m)), B(("601", -80m))));
            Assert.False(PortfoyHesaplayici.GelirDustu(B(("600", -900m)), null)); // önceki ay yüklenmemiş

            // Ocak'ta karşılaştırma yok (kümülatif yeniden başlar).
            Assert.DoesNotContain(PortfoyHesaplayici.GelirDusukUyarisi,
                PortfoyHesaplayici.Uyarilar(1, B(("600", -10m)), B(("600", -1000m)), 0m));
        }

        // ---- yükleme ----

        [Fact]
        public async Task Onizleme_yazmaz_ve_kayitla_ayni_sonucu_verir()
        {
            using var db = Context();
            var servis = Servis(db);

            var onizleme = await servis.OnizleAsync(Istek(Lime, 2026, 9, Dengeli()));
            Assert.Equal(0, await db.AylikMizanlar.CountAsync());
            Assert.False(onizleme.Kaydedildi);

            var kayit = await servis.KaydetAsync(Istek(Lime, 2026, 9, Dengeli()));
            Assert.True(kayit.Kaydedildi);

            Assert.Equal(onizleme.NetSatisKumulatif, kayit.NetSatisKumulatif);
            Assert.Equal(990m, kayit.NetSatisKumulatif);
            Assert.Equal(400m, kayit.SmmKumulatif);
            Assert.Equal(150m, kayit.FaaliyetGiderleriKumulatif);
            Assert.True(kayit.Dengeli);
            Assert.Empty(kayit.Uyarilar);

            var m = await db.AylikMizanlar.Include(x => x.Satirlar).SingleAsync();
            Assert.Equal(8, m.SatirSayisi);
            Assert.Equal(-1000m, m.Satirlar.Single(s => s.HesapKodu == "600").Bakiye);
            Assert.Equal("test", m.YukleyenKullanici);
            Assert.Equal(GiderYontemi.YediB, m.GiderYontemi);
        }

        [Fact]
        public async Task Dengesiz_mizan_kaydedilir_ama_sorunlu_isaretlenir()
        {
            using var db = Context();
            var satirlar = Dengeli();
            satirlar.Add(S("191", 75m, 0m));

            var sonuc = await Servis(db).KaydetAsync(Istek(Lime, 2026, 9, satirlar));

            Assert.True(sonuc.Kaydedildi);
            Assert.False(sonuc.Dengeli);
            Assert.Equal(75m, sonuc.DengeFarki);
            Assert.Contains(sonuc.Uyarilar, u => u.StartsWith("Mizan dengesiz"));

            var m = await db.AylikMizanlar.SingleAsync();
            Assert.False(m.Dengeli);
            Assert.Equal(75m, m.DengeFarki);
        }

        [Fact]
        public async Task Ayni_ay_onaysiz_tekrar_yuklenmez_onayla_eskisinin_yerine_gecer()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.KaydetAsync(Istek(Lime, 2026, 9, Dengeli()));

            var onizleme = await servis.OnizleAsync(Istek(Lime, 2026, 9, Dengeli(1200m)));
            Assert.NotNull(onizleme.Mevcut);
            Assert.Equal("mizan-9.xlsx", onizleme.Mevcut!.DosyaAdi);

            var ex = await Assert.ThrowsAsync<AyniAyVarException>(() => servis.KaydetAsync(Istek(Lime, 2026, 9, Dengeli(1200m))));
            Assert.NotNull(ex.Onizleme.Mevcut);
            Assert.Equal(-1000m, db.AylikMizanSatirlari.Single(s => s.HesapKodu == "600").Bakiye);

            var yeni = Dengeli(1200m);
            yeni.RemoveAll(s => s.HesapKodu == "632");
            yeni.Add(S("632", 150m, 0m));
            yeni.Add(S("101", 0m, 0m));
            await servis.KaydetAsync(Istek(Lime, 2026, 9, yeni, uzerineYaz: true));

            var m = await db.AylikMizanlar.Include(x => x.Satirlar).SingleAsync();
            Assert.Equal(9, m.SatirSayisi);
            Assert.Equal(9, await db.AylikMizanSatirlari.CountAsync());
            Assert.Equal(-1200m, m.Satirlar.Single(s => s.HesapKodu == "600").Bakiye);
        }

        [Fact]
        public async Task Pasif_firmanin_mizani_reddedilir()
        {
            using var db = Context();
            var ex = await Assert.ThrowsAsync<FirmaPortfoyuKuralException>(
                () => Servis(db).KaydetAsync(Istek(Pkf, 2026, 9, Dengeli())));

            Assert.Equal("firmaId", ex.Field);
            Assert.Contains("pasif", ex.Message);
            Assert.Equal(0, await db.AylikMizanlar.CountAsync());
        }

        [Theory]
        [InlineData("1020")]
        [InlineData("120 1")]
        [InlineData("ABC")]
        public async Task Uc_haneli_olmayan_kod_reddedilir(string kod)
        {
            using var db = Context();
            var satirlar = Dengeli();
            satirlar.Add(S(kod, 0m, 0m));

            var ex = await Assert.ThrowsAsync<FirmaPortfoyuKuralException>(() => Servis(db).OnizleAsync(Istek(Lime, 2026, 9, satirlar)));
            Assert.Contains("3 haneli", ex.Message);
        }

        [Fact]
        public async Task Ayni_kod_iki_kez_ve_gelecek_ay_reddedilir()
        {
            using var db = Context();
            var cift = Dengeli();
            cift.Add(S("600", 0m, 1m));
            await Assert.ThrowsAsync<FirmaPortfoyuKuralException>(() => Servis(db).OnizleAsync(Istek(Lime, 2026, 9, cift)));

            var ex = await Assert.ThrowsAsync<FirmaPortfoyuKuralException>(() => Servis(db).OnizleAsync(Istek(Lime, 2026, 11, Dengeli())));
            Assert.Contains("Gelecek", ex.Message);
        }

        [Fact]
        public async Task Gelir_tablosu_hesabi_yoksa_kaydedilir_uyarilir_sorunlu_sayilmaz()
        {
            using var db = Context();
            var sonuc = await Servis(db).KaydetAsync(Istek(Lime, 2026, 9, new() { S("102", 100m, 0m), S("500", 0m, 100m) }));

            Assert.True(sonuc.Kaydedildi);
            Assert.True(sonuc.Dengeli);
            Assert.False(sonuc.GelirTablosuVar);
            Assert.Null(sonuc.NetSatisKumulatif);
            Assert.Contains(PortfoyHesaplayici.GelirTablosuYokUyarisi, sonuc.Uyarilar);

            var hucre = (await Servis(db).DurumAsync(2026)).Firmalar.Single(f => f.FirmaId == Lime).Aylar[8];
            Assert.True(hucre.Yuklu);
            Assert.True(hucre.Dengeli);
        }

        [Fact]
        public async Task Gelir_onceki_aydan_dusukse_kayitta_ve_matriste_uyarir()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.KaydetAsync(Istek(Lime, 2026, 8, Dengeli(1000m)));

            var sonuc = await servis.KaydetAsync(Istek(Lime, 2026, 9, Dengeli(800m)));
            Assert.Contains(PortfoyHesaplayici.GelirDusukUyarisi, sonuc.Uyarilar);
            Assert.True(sonuc.Dengeli);

            var eylul = (await servis.DurumAsync(2026)).Firmalar.Single(f => f.FirmaId == Lime).Aylar[8];
            Assert.Contains(PortfoyHesaplayici.GelirDusukUyarisi, eylul.Uyarilar);
        }

        [Fact]
        public async Task Kapanis_sonrasi_Aralik_isaretlenir()
        {
            using var db = Context();
            var sonuc = await Servis(db).KaydetAsync(Istek(Lime, 2025, 12,
                new() { S("102", 600m, 0m), S("500", 0m, 200m), S("590", 0m, 400m) }));

            Assert.True(sonuc.KapanisSonrasi);
            Assert.Contains(PortfoyHesaplayici.KapanisSonrasiUyarisi, sonuc.Uyarilar);
            Assert.True((await db.AylikMizanlar.SingleAsync()).KapanisSonrasiMi);
        }

        [Fact]
        public async Task Durum_matrisi_yalniz_aktif_firmalar_12_ay()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.KaydetAsync(Istek(Lime, 2026, 8, Dengeli()));
            var dengesiz = Dengeli();
            dengesiz.Add(S("191", 5m, 0m));
            await servis.KaydetAsync(Istek(Lime, 2026, 9, dengesiz));
            await servis.KaydetAsync(Istek(Dgr, 2025, 9, Dengeli()));   // başka yıl

            var durum = await servis.DurumAsync(2026);

            Assert.Equal(new[] { "DGR", "LIME" }, durum.Firmalar.Select(f => f.KisaAd));   // PKF pasif → yok
            var lime = durum.Firmalar.Single(f => f.FirmaId == Lime);
            Assert.Equal(12, lime.Aylar.Count);
            Assert.Equal(2, lime.YukluAySayisi);
            Assert.Equal("Cengiz", lime.Sorumlu);
            Assert.True(lime.Aylar[7].Yuklu && lime.Aylar[7].Dengeli);
            Assert.True(lime.Aylar[8].Yuklu);
            Assert.False(lime.Aylar[8].Dengeli);
            Assert.False(lime.Aylar[9].Yuklu);
            Assert.Equal(0, durum.Firmalar.Single(f => f.FirmaId == Dgr).YukluAySayisi);
        }

        [Fact]
        public async Task Silme_yalniz_o_firma_ayini_siler()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.KaydetAsync(Istek(Lime, 2026, 8, Dengeli()));
            await servis.KaydetAsync(Istek(Lime, 2026, 9, Dengeli()));

            await servis.SilAsync(Lime, 2026, 9);

            var kalan = await db.AylikMizanlar.SingleAsync();
            Assert.Equal(8, kalan.Ay);
            Assert.Equal(8, await db.AylikMizanSatirlari.CountAsync());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => servis.SilAsync(Lime, 2026, 9));
        }
    }
}
