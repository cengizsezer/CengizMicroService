using WebApp.Application.Services;
using WebApp.Domain.Models.FirmaKontrol;
using static WebApp.Application.Services.DikeyYuzdeSunumu;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// PROMPT 4 — Dikey Yüzdeler sunum modeli. Gerçek hesap planı üzerinde, madde 9'daki
    /// toplamları veren sentetik bir mizanla. Ham bakiyeler borç-pozitif (borç − alacak).
    /// </summary>
    public class DikeyYuzdeSunumuTests
    {
        private static readonly Dictionary<string, decimal?> Bos = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gelir tablosu: Gelirler 22.968.518,68 · iadeler 666.790,50 · Giderler 10.924.370,50.</summary>
        private static Dictionary<string, decimal?> GelirMizani() => new(StringComparer.OrdinalIgnoreCase)
        {
            // GELİRLER (alacak → ham eksi)
            ["600"] = -21_458_746.99m,
            ["601"] = -1_228_856.20m,
            ["642"] = -100_000.00m,
            ["645"] = -80_000.00m,
            ["646"] = -100_000.00m,
            ["679"] = -915.49m,

            // Satış iadeleri (borç)
            ["610"] = 666_790.50m,

            // GİDERLER — 622/631/632 boş, tutarlar 7'li yansıtmada (740/760/770)
            ["740"] = 5_000_000.00m,
            ["760"] = 1_000_000.00m,
            ["770"] = 4_000_000.00m,
            ["654"] = 500_000.00m,
            ["656"] = 400_000.00m,
            ["689"] = 24_370.50m,
        };

        /// <summary>Bilanço: Aktif 16.550.308,93 · Pasif 23.673.764,00.</summary>
        private static Dictionary<string, decimal?> BilancoMizani() => new(StringComparer.OrdinalIgnoreCase)
        {
            ["102"] = 57_558.81m,
            ["120"] = 10_120_346.89m,
            ["136"] = 3_846_996.20m,
            ["190"] = 699_237.52m,
            ["193"] = 1_165_312.89m,
            ["255"] = 900_000.00m,
            ["257"] = -300_000.00m,
            ["264"] = 60_856.62m,

            ["320"] = -12_008_789.15m,
            ["500"] = -11_664_974.85m,
        };

        private static HesapPlani Plan(Dictionary<string, decimal?> raw)
        {
            var plan = HesapPlaniTestVerisi.Yukle();
            HesapPlaniTestVerisi.MizanUygula(plan, raw);
            return plan;
        }

        private static HesapDugumu Dugum(string kod, string ad, decimal ham) =>
            HesapDugumu.Olustur(kod, ad, 0m, 0m, ham)!;

        private static List<HesapDugumu> Agac() => new()
        {
            Dugum("600", "Yurtiçi Satışlar", -21_458_746.99m),
            Dugum("600 1", "Hizmet Satışları", -21_458_746.99m),      // tek çocuk, aynı tutar → atlanır
            Dugum("600 1 20", "Kurumsal Strateji", -12_194_054.99m),
            Dugum("600 1 21", "PKF Aday", -9_264_692.00m),

            Dugum("770", "Genel Yönetim Giderleri", 4_000_000.00m),
            Dugum("770 1", "Çeşitli Giderler", 1_457_943.41m),
            Dugum("770 1 01", "Ortak Alan Giderleri", 1_416_054.75m),
            Dugum("770 1 02", "Diğer", 41_888.66m),
            Dugum("770 2", "Dışarıdan Sağlanan Fayda ve Hizmet", 739_128.04m),
            Dugum("770 2 01", "SMMM Ücretleri", 739_128.04m),
            Dugum("770 3", "Personel Giderleri", 1_802_928.55m),
        };

        private static OzetGrafikSonucu Ozet(IReadOnlyList<HesapDugumu>? agac = null)
        {
            var gelir = GelirMizani();
            var bil = BilancoMizani();
            var hepsi = gelir.Concat(bil).ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);
            var plan = Plan(hepsi);

            return OzetGrafik(plan.GelirTablosu, plan.Aktif, plan.Pasif, hepsi, Bos,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                agac ?? Agac(), altKirilim: true, sifirlar: false);
        }

        // ── Dört bölüm birbirini kapatmalı ──

        [Fact]
        public void Dort_bolum_kapanir_gelirler_eksi_iadeler_eksi_giderler_ticari_kara_esittir()
        {
            var raw = GelirMizani();
            var plan = Plan(raw);
            var ozet = Ozet();

            Assert.Equal(22_968_518.68m, ozet.Gelirler.Toplam);
            Assert.Equal(666_790.50m, ozet.SatisIadeleri);
            Assert.Equal(10_924_370.50m, ozet.Giderler.Toplam);
            Assert.Equal(11_377_357.68m, ozet.TicariKar);

            // Aynı mizandan gelir tablosunun 690'ı — ZORUNLU eşitlik.
            var gt = GelirTablosuCalculator.Compute(plan.GelirTablosu, raw, Bos);
            var (_, k690) = GelirTablosuCalculator.GetDonemKari(gt);
            Assert.Equal(k690, ozet.TicariKar);

            // Yansıtma: 6'lı boşken kalem 7'li hesapla görünür, 6'lı ayrıca sayılmaz.
            var giderKodlari = ozet.Giderler.Kalemler.Select(k => k.Kod).ToList();
            Assert.Contains("770", giderKodlari);
            Assert.DoesNotContain("632", giderKodlari);
            Assert.Equal(new[] { "600", "601", "642", "645", "646", "679" },
                ozet.Gelirler.Kalemler.Select(k => k.Kod).OrderBy(k => k).ToArray());
        }

        [Fact]
        public void Varliklar_ve_kaynaklar_bilanco_toplamlarina_esittir_kontra_netlenir()
        {
            var ozet = Ozet();

            Assert.Equal(16_550_308.93m, ozet.Varliklar.Toplam);
            Assert.Equal(23_673_764.00m, ozet.Kaynaklar.Toplam);

            var demirbas = ozet.Varliklar.Kalemler.Single(k => k.Kod == "255");
            Assert.Equal("Demirbaşlar (net)", demirbas.Ad);
            Assert.Equal(600_000m, demirbas.Tutar);
            Assert.DoesNotContain(ozet.Varliklar.Kalemler, k => k.Kod == "257");

            // Büyükten küçüğe
            Assert.Equal("120", ozet.Varliklar.Kalemler[0].Kod);

            Assert.Equal(KalemTuru.Ozkaynak, ozet.Kaynaklar.Kalemler.Single(k => k.Kod == "500").Tur);
            Assert.Equal(KalemTuru.Borc, ozet.Kaynaklar.Kalemler.Single(k => k.Kod == "320").Tur);
        }

        [Fact]
        public void Alt_kirilim_gecis_dugumlerini_atlar_ve_en_fazla_iki_seviye_iner()
        {
            var ozet = Ozet();

            var k600 = ozet.Gelirler.Kalemler.Single(k => k.Kod == "600").AltKirilimlar;
            Assert.DoesNotContain(k600, a => a.Kod == "600 1");
            Assert.Equal(12_194_054.99m, k600.Single(a => a.Ad == "Kurumsal Strateji").Tutar);
            Assert.Equal(9_264_692.00m, k600.Single(a => a.Ad == "PKF Aday").Tutar);

            var k770 = ozet.Giderler.Kalemler.Single(k => k.Kod == "770").AltKirilimlar;
            var cesitli = k770.Single(a => a.Ad == "Çeşitli Giderler");
            Assert.Equal(1_457_943.41m, cesitli.Tutar);
            Assert.Equal(1, cesitli.Seviye);
            Assert.Equal(1_416_054.75m, k770.Single(a => a.Ad == "Ortak Alan Giderleri" && a.Seviye == 2).Tutar);
            Assert.Equal(739_128.04m, k770.Single(a => a.Ad == "SMMM Ücretleri" && a.Seviye == 2).Tutar);

            // Alt kırılım toplama girmez: gider toplamı aynı.
            Assert.Equal(10_924_370.50m, ozet.Giderler.Toplam);
        }

        [Fact]
        public void Agac_yoksa_alt_kirilim_cizilmez_hata_verilmez()
        {
            var ozet = Ozet(Array.Empty<HesapDugumu>());

            Assert.True(ozet.AgacYok);
            Assert.All(ozet.Gelirler.Kalemler, k => Assert.Empty(k.AltKirilimlar));
            Assert.Equal(22_968_518.68m, ozet.Gelirler.Toplam);
        }

        // ── Görünüm 1: bilanço yüzdeleri ve taban uyarısı ──

        [Fact]
        public void Bilanco_yuzdeleri_kendi_bolum_toplamina_oranlanir_ve_fark_uyarisi_cikar()
        {
            var plan = Plan(BilancoMizani());
            var b = Bilanco(plan.Aktif, plan.Pasif);

            Assert.Equal(16_550_308.93m, b.AktifToplam);
            Assert.Equal(23_673_764.00m, b.PasifToplam);
            Assert.Equal(-7_123_455.07m, b.Fark);
            Assert.True(b.FarkVar);

            decimal Yuzde(string ad) => Math.Round(b.Satirlar.First(s => s.Ad == ad).Yuzde!.Value, 2);
            decimal HesapYuzde(string kod) => Math.Round(b.Satirlar.First(s => s.Kod == kod).Yuzde!.Value, 2);

            Assert.Equal(96.01m, Yuzde("I -DÖNEN VARLIKLAR"));
            Assert.Equal(3.99m, Yuzde("II -DURAN VARLIKLAR"));
            Assert.Equal(61.15m, HesapYuzde("120"));
            Assert.Equal(23.24m, HesapYuzde("136"));
            Assert.Equal(7.04m, HesapYuzde("193"));
            Assert.Equal(0.35m, HesapYuzde("102"));
            Assert.Equal(50.73m, Yuzde("III-KISA VADELİ YABANCI KAYNAKLAR"));
            Assert.Equal(49.27m, Yuzde("V-ÖZ KAYNAKLAR"));

            Assert.Equal(100m, b.Satirlar.Single(s => s.Ad == "AKTİF TOPLAM").Yuzde);

            // AKTİF ve PASİF aynı listede, alt alta.
            var bolumler = b.Satirlar.Where(s => s.Tur == SatirTuru.Bolum).Select(s => s.Ad).ToArray();
            Assert.Equal(new[] { "AKTİF", "PASİF" }, bolumler);
        }

        // ── Görünüm 2: net satışa oran ──

        [Fact]
        public void Gelir_tablosu_net_satislara_oranlanir()
        {
            var raw = GelirMizani();
            var g = GelirTablosu(Plan(raw).GelirTablosu, raw, Bos, null);

            Assert.Equal(22_020_812.69m, g.NetSatislar);
            Assert.Equal(11_377_357.68m, g.TicariKar);

            decimal Yuzde(string ad) => Math.Round(g.Satirlar.First(s => s.Ad == ad).Yuzde!.Value, 2);
            Assert.Equal(103.03m, Yuzde("A-BRÜT SATIŞLAR"));
            Assert.Equal(100.00m, Yuzde("C-NET SATIŞLAR"));

            Assert.Equal(SatirTuru.TicariKar, g.Satirlar.Single(s => s.Kod == "690").Tur);
            Assert.Equal(SatirTuru.DonemNetKari, g.Satirlar.Single(s => s.Kod == "692").Tur);
        }

        // ── İki çubuk kuralı ──

        [Fact]
        public void Iki_cubuk_kurali_farklidir()
        {
            // Görünüm 1-2: |dikey %|, tam genişlik %100 (103 → 100'de kesilir).
            Assert.Equal(61.15m, CubukDikey(61.15m));
            Assert.Equal(3m, CubukDikey(-3m));
            Assert.Equal(100m, CubukDikey(103.03m));

            // Özet: bölümün en büyük kalemine göre.
            Assert.Equal(100m, CubukBolumEnBuyugu(500m, 500m));
            Assert.Equal(50m, CubukBolumEnBuyugu(250m, 500m));
        }
    }
}
