using WebApp.Application.Services;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Grup toplamlarının TEK kaynaktan gelmesi. Bilanço, Bilanço Özet, Dikey Yüzdeler
    /// ve Finansal Oranlar sekmeleri aynı hesaplayıcıyı kullanır; gelir tablosu da
    /// gruplama/toplama için MizanHesaplayici'ye devreder.
    ///
    /// Regresyon: GelirTablosuCalculator'ın kendi MainGroup sınır kuralı vardı ve
    /// Finansal Oranlar'da "I -DÖNEN VARLIKLAR" Bilanço Özet'tekinden farklı çıkıyordu —
    /// hesap planında yanlışlıkla MainGroup etiketli olan "H-Diğer Dönen Varlıklar"
    /// (190-199) alt bölümünde toplam erken kesiliyordu.
    /// </summary>
    public class GrupToplamiTekKaynakTests
    {
        private static decimal? Ad(List<MizanHesaplayici.ComputedRow> rows, string ad) =>
            rows.First(r => string.Equals(r.Source.Ad?.Trim(), ad, StringComparison.OrdinalIgnoreCase)).Cari;

        private static List<MizanHesaplayici.ComputedRow> AktifHesapla()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            HesapPlaniTestVerisi.MizanUygula(plan, new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["100"] = 5_000m,    // Kasa            — A-Hazır Değerler (SubGroup)
                ["190"] = 2_000m,    // Devreden KDV    — H-Diğer Dönen Varlıklar (MainGroup etiketli alt bölüm)
                ["252"] = 10_000m,   // Binalar         — D-Maddi Duran Varlıklar
            });

            return MizanHesaplayici.Compute(plan.Aktif, MaliTabloBolumu.Aktif);
        }

        [Fact]
        public void DonenVarliklar_YanlisMainGroupEtiketli_AltBolumu_Emer()
        {
            var aktif = AktifHesapla();

            // 190 Devreden KDV dönen varlıktır → I -DÖNEN VARLIKLAR'a dahil olmalı.
            // Eski GelirTablosuCalculator kuralı burada keserek 5.000 veriyordu.
            Assert.Equal(7_000m, Ad(aktif, "I -DÖNEN VARLIKLAR"));
            Assert.Equal(10_000m, Ad(aktif, "II -DURAN VARLIKLAR"));
        }

        [Fact]
        public void DonenArtiDuran_AktifGenelToplamina_Esit()
        {
            var aktif = AktifHesapla();

            var donen = Ad(aktif, "I -DÖNEN VARLIKLAR")!.Value;
            var duran = Ad(aktif, "II -DURAN VARLIKLAR")!.Value;

            // Finansal Oranlar "Ortalama Aktif"i Dönen + Duran olarak kurar; grup sınırı
            // doğru olmazsa bu toplam genel toplamı tutmaz.
            Assert.Equal(Ad(aktif, "AKTİF (VARLIKLAR) TOPLAMI"), donen + duran);
        }

        [Fact]
        public void Pasif_YanlisMainGroupEtiketli_AltBolumu_Emer()
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            HesapPlaniTestVerisi.MizanUygula(plan, new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["320"] = -8_000m,   // Satıcılar     — B-Ticari Borçlar (SubGroup)
                ["391"] = -1_500m,   // Hesaplanan KDV — I-Diğer Kısa Vadeli Yabancı Kaynaklar (MainGroup etiketli)
                ["500"] = -25_000m,  // Sermaye
            });

            var pasif = MizanHesaplayici.Compute(plan.Pasif, MaliTabloBolumu.Pasif);

            Assert.Equal(9_500m, Ad(pasif, "III-KISA VADELİ YABANCI KAYNAKLAR"));
            Assert.Equal(34_500m, Ad(pasif, "PASİF(KAYNAKLAR) TOPLAMI"));
        }

        [Fact]
        public void Iki_GirisNoktasi_MainGroup_Sinirinda_Ayrismaz()
        {
            // Yapısal regresyon: GelirTablosuCalculator'ın ikinci bir gruplama kuralı
            // OLMADIĞINI sabitler. Aktif bölümü, MainGroup sınır kuralını tetikleyen tek
            // veri kümesi olduğu için burada kasıtlı olarak gelir tablosu hesaplayıcısına
            // verilir (uygulamada böyle bir çağrı yok — Aktif yalnızca MizanHesaplayici'den
            // geçer). Gelir tablosu ve Pasif aynı işaret yönünü kullandığından değerler
            // birebir eşit olmalı; eski kural "I -DÖNEN VARLIKLAR"da erken keserek
            // farklı sonuç veriyordu.
            var plan = HesapPlaniTestVerisi.Yukle();
            var raw = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["100"] = 5_000m,
                ["190"] = 2_000m,
                ["252"] = 10_000m,
            };
            HesapPlaniTestVerisi.MizanUygula(plan, raw);

            var bos = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var gelirMotoru = GelirTablosuCalculator.Compute(plan.Aktif, raw, bos);
            var mizanMotoru = MizanHesaplayici.Compute(plan.Aktif, MaliTabloBolumu.Pasif);

            for (int i = 0; i < plan.Aktif.Count; i++)
            {
                Assert.Equal(mizanMotoru[i].Cari, gelirMotoru[i].Cari);
                Assert.Equal(mizanMotoru[i].Onceki, gelirMotoru[i].OncekiCari);
            }
        }

        [Fact]
        public void GelirTablosu_Gruplama_Ayni_Motordan_Gelir()
        {
            // GelirTablosuCalculator gruplama/toplama için MizanHesaplayici'ye devreder;
            // yansıtma fallback'i ve 690/691/692 dışındaki her satır aynı çıkmalı.
            var plan = HesapPlaniTestVerisi.Yukle();
            var raw = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["600"] = -1_000m,
                ["632"] = 400m,
            };
            HesapPlaniTestVerisi.MizanUygula(plan, raw);

            var bos = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
            var gelirTablosu = GelirTablosuCalculator.Compute(plan.GelirTablosu, raw, bos);
            var motor = MizanHesaplayici.Compute(plan.GelirTablosu, MaliTabloBolumu.GelirTablosu);

            foreach (var kod in new[] { "600", "632" })
            {
                Assert.Equal(
                    motor.First(r => r.Source.Kod == kod).Cari,
                    gelirTablosu.First(r => r.Source.Kod == kod).Cari);
            }

            foreach (var ad in new[] { "A-BRÜT SATIŞLAR", "E-FAALİYET GİDERLERİ (-)", "FAALİYET KARI VEYA ZARARI" })
            {
                Assert.Equal(
                    Ad(motor, ad),
                    gelirTablosu.First(r => string.Equals(r.Source.Ad?.Trim(), ad, StringComparison.OrdinalIgnoreCase)).Cari);
            }
        }
    }
}
