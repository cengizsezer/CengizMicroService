using WebApp.Application.Services;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Gelir tablosu hesaplama mantığı — gerçek hesap planı (wwwroot/data/hesap_plani.json)
    /// üzerinden. İki kural doğrulanır:
    ///   1) İşaret yönü: gelir hesapları artı, gider/maliyet hesapları eksi.
    ///   2) Ara toplamlar kümülatif birikir (net satışlar → brüt kâr → faaliyet kârı →
    ///      olağan kâr → 690).
    /// </summary>
    public class GelirTablosuCalculatorTests
    {
        // DOĞRULAMA SENARYOSU — mizanda yalnızca bu dört hesap var.
        // Ham bakiyeler borç-pozitif konvansiyonda (borç − alacak):
        //   740 borç  983.287,60  → yansıtma: 622 Satılan Hizmet Maliyeti
        //   770 borç   63.470,80  → yansıtma: 632 Genel Yönetim Giderleri
        //   679 alacak      0,26  → Diğer Olağandışı Gelir ve Kârlar
        //   689 borç        0,01  → Diğer Olağandışı Gider ve Zararlar
        //   600 Yurtiçi Satışlar hesabı YOK.
        private static IReadOnlyDictionary<string, decimal?> SenaryoMizan() =>
            new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["740"] = 983_287.60m,
                ["770"] = 63_470.80m,
                ["679"] = -0.26m,
                ["689"] = 0.01m,
            };

        private static List<GelirTablosuCalculator.ComputedRow> ComputeSenaryo()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            var raw = SenaryoMizan();
            HesapPlaniTestVerisi.MizanUygula(plan, raw);

            return GelirTablosuCalculator.Compute(
                plan.GelirTablosu,
                raw,
                new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase));
        }

        private static decimal? Ad(List<GelirTablosuCalculator.ComputedRow> rows, string ad) =>
            rows.First(r => string.Equals(r.Source.Ad?.Trim(), ad, StringComparison.OrdinalIgnoreCase)).Cari;

        private static decimal? Kod(List<GelirTablosuCalculator.ComputedRow> rows, string kod) =>
            rows.First(r => r.Source.Kod == kod).Cari;

        // ---------------------------------------------------------------- İŞARET

        [Fact]
        public void GelirHesabi_AlacakBakiyesiyle_Arti_Gosterilir()
        {
            var rows = ComputeSenaryo();

            // 679 alacak 0,26 → +0,26 (ham -0,26 DEĞİL).
            Assert.Equal(0.26m, Kod(rows, "679"));
            Assert.Equal(0.26m, Ad(rows, "İ-OLAĞANDIŞI GELİR VE KARLAR"));
        }

        [Fact]
        public void GiderVeMaliyetHesaplari_BorcBakiyesiyle_Eksi_Gosterilir()
        {
            var rows = ComputeSenaryo();

            // 740 → 622 yansıtma fallback'i; maliyet olduğu için eksi.
            Assert.Equal(-983_287.60m, Kod(rows, "622"));
            Assert.Equal(-983_287.60m, Ad(rows, "D-SATIŞLARIN MALİYETİ (-)"));

            // 770 → 632 yansıtma fallback'i.
            Assert.Equal(-63_470.80m, Kod(rows, "632"));
            Assert.Equal(-63_470.80m, Ad(rows, "E-FAALİYET GİDERLERİ (-)"));

            // 689 borç 0,01 → -0,01.
            Assert.Equal(-0.01m, Kod(rows, "689"));
            Assert.Equal(-0.01m, Ad(rows, "J-OLAĞANDIŞI GİDER VE ZARARLAR (-)"));
        }

        [Fact]
        public void BrutSatislar_AlacakBakiyesiyle_Arti_Gosterilir()
        {
            // 600 alacak 1.000,00 → ekranda +1.000,00 (ham -1.000,00 DEĞİL).
            var plan = HesapPlaniTestVerisi.Yukle();
            var raw = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["600"] = -1_000m,
                ["610"] = 100m,   // Satıştan İadeler — borç bakiyeli kontra gelir
            };
            HesapPlaniTestVerisi.MizanUygula(plan, raw);

            var rows = GelirTablosuCalculator.Compute(
                plan.GelirTablosu, raw,
                new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase));

            Assert.Equal(1_000m, Kod(rows, "600"));
            Assert.Equal(-100m, Kod(rows, "610"));
            Assert.Equal(1_000m, Ad(rows, "A-BRÜT SATIŞLAR"));
            Assert.Equal(-100m, Ad(rows, "B-SATIŞ İNDİRİMLERİ (-)"));

            // C Net satışlar = A brüt satışlar − B satış indirimleri
            Assert.Equal(900m, Ad(rows, "C-NET SATIŞLAR"));
        }

        // ----------------------------------------------------------- ARA TOPLAMLAR

        [Fact]
        public void AraToplamlar_Kumulatif_Birikir_DogrulamaSenaryosu()
        {
            var rows = ComputeSenaryo();

            // Brüt satış zararı = C net satışlar (yok) − D satışların maliyeti
            Assert.Equal(-983_287.60m, Ad(rows, "BRÜT SATIŞ KARI VEYA ZARARI"));

            // Faaliyet zararı = brüt satış zararı − E faaliyet giderleri
            Assert.Equal(-1_046_758.40m, Ad(rows, "FAALİYET KARI VEYA ZARARI"));

            // Olağan zarar = faaliyet zararı + F,G − H (bu senaryoda hiçbiri yok)
            Assert.Equal(-1_046_758.40m, Ad(rows, "OLAĞAN KAR VEYA ZARAR"));

            // 690 Ticari zarar = olağan zarar + İ olağandışı gelirler − J olağandışı giderler
            Assert.Equal(-1_046_758.15m, Kod(rows, "690"));
        }

        [Fact]
        public void AraToplamlar_KendindenOncekiBolumu_TekrarEtmez()
        {
            var rows = ComputeSenaryo();

            // Regresyon: eski mantıkta her ara toplam yalnızca bir önceki Total'dan
            // sonraki hesapları topluyordu → 690, İ ve J bölümünün net'ini (-0,25)
            // gösteriyor, önceki zararı taşımıyordu.
            Assert.NotEqual(-0.25m, Kod(rows, "690"));

            // Faaliyet kârı yalnızca E bölümünü değil, D'yi de içermeli.
            Assert.NotEqual(Ad(rows, "E-FAALİYET GİDERLERİ (-)"), Ad(rows, "FAALİYET KARI VEYA ZARARI"));
        }

        [Fact]
        public void Yansitma_Hesaplari_KumulatifToplama_Girmez()
        {
            var rows = ComputeSenaryo();

            // 740/770 tutarları 622/632'ye taşındı; yansıtma satırları bilgi amaçlı
            // görünür ama 690'a ikinci kez eklenmez (çifte sayım olmaz).
            Assert.Equal(-983_287.60m, Kod(rows, "740"));
            Assert.Equal(-63_470.80m, Kod(rows, "770"));
            Assert.Equal(-1_046_758.15m, Kod(rows, "690"));
        }

        // -------------------------------------------------------- VERGİ HESAPLAMASI

        [Fact]
        public void VergiPaneline_Giden_TicariKar_GelirTablosuyla_Ayni()
        {
            var rows = ComputeSenaryo();

            // Vergi Hesaplaması sekmesi 690'ı buradan alır (Detay.razor → TicariKar).
            var (_, cari) = GelirTablosuCalculator.GetDonemKari(rows);

            Assert.Equal(-1_046_758.15m, cari);
            Assert.Equal(Kod(rows, "690"), cari);
        }

        [Fact]
        public void Mizanda_690_Varsa_Isaretlenerek_Formulun_Onune_Gecer()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            var raw = new Dictionary<string, decimal?>(SenaryoMizan(), StringComparer.OrdinalIgnoreCase)
            {
                // Muhasebeci kapanışı yapmış: 690 alacak bakiyeli 500,00 (kâr).
                ["690"] = -500m,
            };
            HesapPlaniTestVerisi.MizanUygula(plan, raw);

            var rows = GelirTablosuCalculator.Compute(
                plan.GelirTablosu, raw,
                new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase));

            Assert.Equal(500m, Kod(rows, "690"));
        }

        [Fact]
        public void Hesaplanan_691_Vergi_Eksi_Yazilir_Ve_692_690_Ile_Toplanir()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            var raw = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["600"] = -1_000m,
            };
            HesapPlaniTestVerisi.MizanUygula(plan, raw);

            var rows = GelirTablosuCalculator.Compute(
                plan.GelirTablosu, raw,
                new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase),
                hesaplananVergi691Cari: 250m);

            Assert.Equal(1_000m, Kod(rows, "690"));
            Assert.Equal(-250m, Kod(rows, "691"));
            Assert.Equal(750m, Kod(rows, "692"));
        }
    }
}
