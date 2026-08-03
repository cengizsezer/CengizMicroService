using WebApp.Application.Services;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Aktif / Pasif (ve aynı bileşeni kullanan Dikey Yüzdeler Analizi) işaret yönü.
    /// Aktif borç bakiyeli → ham değer; Pasif alacak bakiyeli → ters çevrilir.
    /// </summary>
    public class MizanHesaplayiciIsaretTests
    {
        private static decimal? Ad(List<MizanHesaplayici.ComputedRow> rows, string ad) =>
            rows.First(r => string.Equals(r.Source.Ad?.Trim(), ad, StringComparison.OrdinalIgnoreCase)).Cari;

        private static decimal? Kod(List<MizanHesaplayici.ComputedRow> rows, string kod) =>
            rows.First(r => r.Source.Kod == kod).Cari;

        [Fact]
        public void Aktif_BorcBakiyeli_Varliklar_Arti_Kontra_Hesaplar_Eksi()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            HesapPlaniTestVerisi.MizanUygula(plan, new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["100"] = 5_000m,      // Kasa — borç bakiyeli
                ["257"] = -1_200m,     // Birikmiş Amortismanlar (-) — alacak bakiyeli kontra
                ["252"] = 10_000m,     // Binalar
            });

            var rows = MizanHesaplayici.Compute(plan.Aktif, MaliTabloBolumu.Aktif);

            Assert.Equal(5_000m, Kod(rows, "100"));
            Assert.Equal(-1_200m, Kod(rows, "257"));
            Assert.Equal(13_800m, Ad(rows, "AKTİF (VARLIKLAR) TOPLAMI"));
        }

        [Fact]
        public void Pasif_AlacakBakiyeli_Kaynaklar_Arti_Kontra_Hesaplar_Eksi()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            HesapPlaniTestVerisi.MizanUygula(plan, new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["320"] = -8_000m,     // Satıcılar — alacak bakiyeli
                ["500"] = -50_000m,    // Sermaye — alacak bakiyeli
                ["501"] = 20_000m,     // Ödenmemiş Sermaye (-) — borç bakiyeli kontra
            });

            var rows = MizanHesaplayici.Compute(plan.Pasif, MaliTabloBolumu.Pasif);

            Assert.Equal(8_000m, Kod(rows, "320"));
            Assert.Equal(50_000m, Kod(rows, "500"));
            Assert.Equal(-20_000m, Kod(rows, "501"));

            // Özkaynaklar = 50.000 − 20.000; Pasif toplam da artı olmalı (eksi DEĞİL).
            Assert.Equal(30_000m, Ad(rows, "V-ÖZ KAYNAKLAR"));
            Assert.Equal(38_000m, Ad(rows, "PASİF(KAYNAKLAR) TOPLAMI"));
        }

        [Fact]
        public void Denk_Mizanda_Aktif_Ve_Pasif_Toplamlari_Esit_Isaretle_Cikar()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            var raw = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["100"] = 40_000m,     // Kasa (borç)
                ["320"] = -15_000m,    // Satıcılar (alacak)
                ["500"] = -25_000m,    // Sermaye (alacak)
            };
            HesapPlaniTestVerisi.MizanUygula(plan, raw);

            var aktif = MizanHesaplayici.Compute(plan.Aktif, MaliTabloBolumu.Aktif);
            var pasif = MizanHesaplayici.Compute(plan.Pasif, MaliTabloBolumu.Pasif);

            Assert.Equal(40_000m, Ad(aktif, "AKTİF (VARLIKLAR) TOPLAMI"));
            Assert.Equal(40_000m, Ad(pasif, "PASİF(KAYNAKLAR) TOPLAMI"));
        }

        [Fact]
        public void DikeyYuzde_Paydasi_Isaret_Duzeltmesinden_Etkilenmez()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            HesapPlaniTestVerisi.MizanUygula(plan, new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["320"] = -30_000m,
                ["500"] = -70_000m,
            });

            var rows = MizanHesaplayici.Compute(plan.Pasif, MaliTabloBolumu.Pasif);
            var toplam = Ad(rows, "PASİF(KAYNAKLAR) TOPLAMI")!.Value;

            // Dikey %: KVYK / Pasif Toplam = %30 — hem pay hem payda artı.
            Assert.Equal(30m, Ad(rows, "III-KISA VADELİ YABANCI KAYNAKLAR")!.Value / toplam * 100m);
        }
    }
}
