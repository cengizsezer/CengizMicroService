using System.Reflection;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaPortfoyu.Dtos;
using CatalogService.Api.Features.FirmaPortfoyu.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.FirmaPortfoyu
{
    /// <summary>
    /// Firma Portföyü Aşama 2: göstergeler ve aylık seri. Gelir tablosu yıl başından kümülatif → aylık = bu ay − önceki
    /// ay; önceki ay yoksa değer yok (tahmin yok). Firmalar arası toplam hiçbir yerde yok. Sabit tarih 13.10.2026.
    /// </summary>
    public class PortfoyGostergeTests
    {
        private const int Lime = 5, Dgr = 3, Pkf = 9;

        private static readonly DateTimeOffset Simdi = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Lime, Unvan = "LIME LTD.", KisaAd = "LIME", VergiKimlikNo = "2222222222", Aktif = true },
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true },
                new Firma { Id = Pkf, Unvan = "PKF ADAY", KisaAd = "PKF ADAY", VergiKimlikNo = "0070511435", Aktif = false });
            db.SaveChanges();
            return db;
        }

        private static FirmaPortfoyuService Servis(CatalogContext db) => new(db, new SahteSaat(Simdi), new SabitKullanici(16));

        private static AylikMizanSatiriDto S(string kod, decimal borc, decimal alacak)
            => new() { HesapKodu = kod, HesapAdi = kod, BorcBakiye = borc, AlacakBakiye = alacak };

        /// <summary>
        /// Dengeli 7/B mizan (kümülatif): satış, SMM, gider; kâr kadar kasa; hazır değer/alacak/borç sabit
        /// düzeltici hesaplarla. Dönem kârı = satış − smm − gider.
        /// </summary>
        private static List<AylikMizanSatiriDto> Mizan(decimal satis, decimal smm, decimal gider, decimal kdv = 0m)
        {
            var kar = satis - smm - gider;
            return new()
            {
                S("100", 50m, 0m), S("102", 1000m + kar, 0m), S("103", 0m, 30m), S("108", 20m, 0m),
                S("120", 400m, 0m), S("121", 100m, 0m), S("122", 0m, 25m),
                S("190", kdv, 0m),
                S("320", 0m, 300m), S("321", 0m, 50m), S("322", 10m, 0m),
                S("500", 0m, 1175m + kdv),
                S("600", 0m, satis), S("621", smm, 0m), S("632", gider, 0m)
            };
        }

        private static async Task Yukle(FirmaPortfoyuService s, int firma, int yil, int ay, List<AylikMizanSatiriDto> satirlar)
        {
            var sonuc = await s.KaydetAsync(new AylikMizanKaydetDto { FirmaId = firma, Yil = yil, Ay = ay, DosyaAdi = "m.xlsx", Satirlar = satirlar });
            Assert.True(sonuc.Dengeli, $"test mizanı dengesiz: {yil}-{ay}");
        }

        [Fact]
        public async Task Aylik_tutar_kumulatif_farktir_ve_bilanco_ayin_bakiyesidir()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2026, 8, Mizan(8000m, 4000m, 1500m, kdv: 40m));
            await Yukle(s, Lime, 2026, 9, Mizan(9200m, 4600m, 1700m, kdv: 0m));

            var p = await s.PortfoyAsync("2026-09", "ay");
            var g = p.Firmalar.Single(f => f.FirmaId == Lime).Gostergeler;

            Assert.Equal(1200m, g.NetSatis.Deger);
            Assert.Equal(600m, g.BrutKar.Deger);                 // 1200 − 600
            Assert.Equal(200m, g.FaaliyetGiderleri.Deger);
            Assert.Equal(400m, g.FaaliyetKari.Deger);
            Assert.Equal(400m, g.DonemKari.Deger);               // aylık: 2900 − 2500
            Assert.Equal(2900m, g.DonemKariYilBasindan.Deger);   // 9200 − 4600 − 1700 (kümülatif)
            Assert.Null(g.DonemKariYilBasindan.Karsilastirma);   // "önceki ay" modunda karşılaştırma yok

            // Bilanço: düzeltici hesaplar işaretle düşer.
            Assert.Equal(50m + 1000m + 2900m - 30m + 20m, g.HazirDegerler.Deger);
            Assert.Equal(475m, g.TicariAlacaklar.Deger);         // 400 + 100 − 25
            Assert.Equal(340m, g.TicariBorclar.Deger);           // 300 + 50 − 10
            Assert.Equal(0m, g.DevredenKdv.Deger);
            Assert.Equal(40m, g.DevredenKdv.Karsilastirma);

            // Karşılaştırma = Ağustos'un AYLIK değeri — Temmuz yok → değer yok.
            Assert.Null(g.NetSatis.Karsilastirma);
        }

        [Fact]
        public async Task Onceki_ay_eksikse_aylik_deger_yok_tahmin_yapilmaz()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2026, 7, Mizan(7000m, 3500m, 1200m));
            await Yukle(s, Lime, 2026, 9, Mizan(9000m, 4500m, 1600m));   // Ağustos yok

            var f = (await s.PortfoyAsync("2026-09", "ay")).Firmalar.Single(x => x.FirmaId == Lime);

            Assert.Null(f.Gostergeler.NetSatis.Deger);
            Assert.Equal(PortfoyHesaplayici.NotOncekiAyEksik, f.Gostergeler.NetSatis.Not);
            Assert.Null(f.Gostergeler.FaaliyetKari.Deger);
            // Kümülatif ve bilanço önceki aya bakmaz.
            Assert.Equal(2900m, f.Gostergeler.DonemKariYilBasindan.Deger);
            Assert.NotNull(f.Gostergeler.HazirDegerler.Deger);
            Assert.Equal(PortfoyMizanDurumu.Tamam, f.Durum);
        }

        [Fact]
        public async Task Ocak_ayinda_aylik_tutar_Ocak_kumulatifidir()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2026, 1, Mizan(1000m, 500m, 200m));

            var g = (await s.PortfoyAsync("2026-01", "ay")).Firmalar.Single(x => x.FirmaId == Lime).Gostergeler;
            Assert.Equal(1000m, g.NetSatis.Deger);
            Assert.Equal(300m, g.FaaliyetKari.Deger);
        }

        [Fact]
        public async Task Gecen_yil_karsilastirmasi_ayni_ayin_aylik_degeri()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2025, 8, Mizan(6000m, 3000m, 1000m));
            await Yukle(s, Lime, 2025, 9, Mizan(6800m, 3400m, 1100m));
            await Yukle(s, Lime, 2026, 8, Mizan(8000m, 4000m, 1500m));
            await Yukle(s, Lime, 2026, 9, Mizan(9200m, 4600m, 1700m));

            var p = await s.PortfoyAsync("2026-09", "yil");
            var g = p.Firmalar.Single(f => f.FirmaId == Lime).Gostergeler;

            Assert.Equal("2025-09", p.KarsilastirmaDonemi);
            Assert.Equal(1200m, g.NetSatis.Deger);
            Assert.Equal(800m, g.NetSatis.Karsilastirma);
            Assert.Equal(100m, g.FaaliyetGiderleri.Karsilastirma);
            Assert.Equal(2300m, g.DonemKariYilBasindan.Karsilastirma);   // geçen yılın aynı dönemi
        }

        [Fact]
        public async Task Ocakta_onceki_ay_karsilastirmasi_gecen_yilin_Araligi()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2025, 11, Mizan(11000m, 5500m, 2000m));
            await Yukle(s, Lime, 2025, 12, Mizan(12500m, 6200m, 2300m));
            await Yukle(s, Lime, 2026, 1, Mizan(900m, 450m, 150m));

            var p = await s.PortfoyAsync("2026-01", "ay");
            var g = p.Firmalar.Single(f => f.FirmaId == Lime).Gostergeler;

            Assert.Equal("2025-12", p.KarsilastirmaDonemi);
            Assert.Equal(900m, g.NetSatis.Deger);
            Assert.Equal(1500m, g.NetSatis.Karsilastirma);
        }

        [Fact]
        public async Task Kapanis_sonrasi_Aralik_gelir_gider_yok_donem_kari_690dan()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2025, 11, Mizan(11000m, 5500m, 2000m));   // Kasım kümülatif kâr 3500
            await Yukle(s, Lime, 2025, 12, new()
            {
                S("102", 5000m, 0m), S("500", 0m, 1000m), S("690", 0m, 4000m)   // yıllık kâr 4000, 6xx kapanmış
            });

            var f = (await s.PortfoyAsync("2025-12", "ay")).Firmalar.Single(x => x.FirmaId == Lime);
            var g = f.Gostergeler;

            Assert.Null(g.NetSatis.Deger);
            Assert.Equal(PortfoyHesaplayici.NotKapanisSonrasi, g.NetSatis.Not);
            Assert.Equal(4000m, g.DonemKariYilBasindan.Deger);
            var aralik = f.Seri.Last();
            Assert.Equal(12, aralik.Ay);
            Assert.Null(aralik.NetSatis);
            Assert.Equal(500m, aralik.DonemKari);   // 4000 − 3500
            Assert.Equal(PortfoyHesaplayici.NotKapanisSonrasi, aralik.Not);
        }

        [Fact]
        public void Donem_kari_7A_ve_yansitma_kurallariyla()
        {
            // 7/A: SMM 740'ta, giderler 760/770'te, finansman 780'de; 751/761/771 yansıtma toplama girmez.
            var b = new Dictionary<string, decimal>
            {
                ["600"] = -10000m, ["610"] = 200m, ["642"] = -300m, ["656"] = 100m,
                ["740"] = 4000m, ["760"] = 1000m, ["770"] = 1500m, ["780"] = 400m,
                ["761"] = -1000m, ["771"] = -1500m, ["690"] = -999m, ["691"] = 100m
            };

            Assert.Equal(9800m, PortfoyHesaplayici.NetSatis(b));
            Assert.Equal(4000m, PortfoyHesaplayici.Smm(b));
            Assert.Equal(2500m, PortfoyHesaplayici.FaaliyetGiderleri(b));
            // 9800 − 4000 − 2500 + 300 − 100 − 400 = 3100 (690/691 dahil değil)
            Assert.Equal(3100m, PortfoyHesaplayici.DonemKari(b));
        }

        [Fact]
        public async Task Durum_eksik_dengesiz_ve_yuklenen_firma_sayisi_yalniz_aktif_firmalar()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2026, 6, Mizan(6000m, 3000m, 1000m));
            var dengesiz = Mizan(9000m, 4500m, 1600m);
            dengesiz.Add(S("191", 5m, 0m));
            await s.KaydetAsync(new AylikMizanKaydetDto { FirmaId = Dgr, Yil = 2026, Ay = 9, DosyaAdi = "d.xlsx", Satirlar = dengesiz });

            var p = await s.PortfoyAsync("2026-09", "ay");

            Assert.Equal(2, p.AktifFirmaSayisi);                  // PKF pasif
            Assert.DoesNotContain(p.Firmalar, f => f.FirmaId == Pkf);
            Assert.Equal(1, p.MizaniYuklenenFirmaSayisi);         // yalnız DGR'nin Eylül'ü var

            var lime = p.Firmalar.Single(f => f.FirmaId == Lime);
            Assert.Equal(PortfoyMizanDurumu.Eksik, lime.Durum);
            Assert.Equal(3, lime.EksikAySayisi);                  // Haziran → Eylül
            Assert.Equal("2026-06", lime.SonMizanDonemi);
            Assert.Equal(PortfoyHesaplayici.NotYuklenmedi, lime.Gostergeler.NetSatis.Not);
            Assert.Null(lime.Gostergeler.HazirDegerler.Deger);

            var dgr = p.Firmalar.Single(f => f.FirmaId == Dgr);
            Assert.Equal(PortfoyMizanDurumu.Dengesiz, dgr.Durum);
            Assert.Contains(dgr.Uyarilar, u => u.StartsWith("Mizan dengesiz"));
        }

        [Fact]
        public async Task Seri_secili_aya_biten_12_ay_ve_varsayilan_donem_son_yuklenen_ay()
        {
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2026, 7, Mizan(7000m, 3500m, 1200m));
            await Yukle(s, Lime, 2026, 8, Mizan(8000m, 4000m, 1500m));

            var p = await s.PortfoyAsync(null, null);

            Assert.Equal("2026-08", p.Donem);
            var seri = p.Firmalar.Single(f => f.FirmaId == Lime).Seri;
            Assert.Equal(12, seri.Count);
            Assert.Equal((2025, 9), (seri[0].Yil, seri[0].Ay));
            Assert.Equal((2026, 8), (seri[11].Yil, seri[11].Ay));
            Assert.Equal(1000m, seri[11].NetSatis);
            Assert.Null(seri[10].NetSatis);                        // Temmuz: Haziran yok
            Assert.Equal(PortfoyHesaplayici.NotOncekiAyEksik, seri[10].Not);
            Assert.Equal(PortfoyHesaplayici.NotYuklenmedi, seri[0].Not);
        }

        [Fact]
        public async Task Yanitta_firmalar_arasi_toplam_alani_yok()
        {
            // Portföy düzeyinde yalnız SAYI var (aktif / mizanı yüklenen firma); hiçbir tutar alanı yok.
            var tutarAlanlari = typeof(PortfoyDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)
                            || p.PropertyType == typeof(GostergeDto) || p.PropertyType == typeof(PortfoyGostergeleriDto)
                            || p.Name.Contains("Toplam", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Name)
                .ToList();
            Assert.Empty(tutarAlanlari);

            // İki firmanın değerleri yanıtta ayrı ayrı durur, birbirine eklenmez.
            using var db = Context();
            var s = Servis(db);
            await Yukle(s, Lime, 2026, 1, Mizan(1000m, 500m, 200m));
            await Yukle(s, Dgr, 2026, 1, Mizan(3000m, 1000m, 500m));
            var p = await s.PortfoyAsync("2026-01", "ay");
            Assert.Equal(new decimal?[] { 3000m, 1000m }, p.Firmalar.Select(f => f.Gostergeler.NetSatis.Deger));
        }

        [Theory]
        [InlineData("2026-13", "ay")]
        [InlineData("26-09", "ay")]
        [InlineData("2026-11", "ay")]   // gelecek ay
        [InlineData("2026-09", "hafta")]
        public async Task Gecersiz_donem_ya_da_karsilastirma_reddedilir(string donem, string kars)
        {
            using var db = Context();
            await Assert.ThrowsAsync<FirmaPortfoyuKuralException>(() => Servis(db).PortfoyAsync(donem, kars));
        }
    }
}
