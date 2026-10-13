using WebApp.Pages.FirmaPortfoyu;
using WebApp.Shared.Dto.FirmaPortfoyu;

namespace WebApp.UnitTests.FirmaPortfoyu
{
    /// <summary>
    /// Grafik verisi sunucunun yanıtından aynen taşınır (hesap yok). Değeri olmayan ay null kalır (çizgide boşluk);
    /// çubuklarda toplam yok; düşen/yükselen yalnız karşılaştırması olan firmalardan, ilk 3'er.
    /// </summary>
    public class PfGrafikVerisiTests
    {
        private static PortfoyFirmasiDto Firma(int id, string ad, decimal? satis, decimal? onceki) => new()
        {
            FirmaId = id,
            KisaAd = ad,
            Gostergeler = new PortfoyGostergeleriDto { NetSatis = new GostergeDto { Deger = satis, Karsilastirma = onceki } }
        };

        [Fact]
        public void Cizgi_seri_aynen_tasinir_bos_ay_null_kalir()
        {
            var f = new PortfoyFirmasiDto
            {
                KisaAd = "LIME",
                Seri =
                {
                    new PortfoyAyDto { Yil = 2026, Ay = 7, Yuklu = true, NetSatis = null, Not = "önceki ay eksik" },
                    new PortfoyAyDto { Yil = 2026, Ay = 8, Yuklu = false, Not = "mizan yüklenmedi" },
                    new PortfoyAyDto { Yil = 2026, Ay = 9, Yuklu = true, NetSatis = 1200m, Smm = 600m, FaaliyetGiderleri = 0m }
                }
            };

            var v = PfGrafikVerisi.Cizgi(f, seciliAy: 9);

            Assert.Equal("cizgi", v.Tur);
            Assert.Equal(new[] { "Net satış", "SMM", "Faaliyet gideri" }, v.Seriler.Select(s => s.Ad));
            Assert.Equal(new[] { "#2a78d6", "#eb6834", "#1baf7a" }, v.Seriler.Select(s => s.Renk));
            Assert.Equal(new decimal?[] { null, null, 1200m }, v.Seriler[0].Degerler);
            Assert.Equal(new decimal?[] { null, null, 0m }, v.Seriler[2].Degerler);   // sıfır gerçek değer, null değil
            Assert.Equal(new[] { "Tem 26", "Ağu 26", "Eyl 26" }, v.Aylar.Select(a => a.Kisa));
            Assert.Equal("Ağustos 2026", v.Aylar[1].Uzun);
            Assert.Equal("mizan yüklenmedi", v.Notlar[1]);
            Assert.Equal(2, v.Secili);
            Assert.EndsWith("2026 Ocak–Aralık", v.Aria);
        }

        [Fact]
        public void Cizgi_yilin_12_ayi_gelecek_ay_bos_secili_ay_vurgulu()
        {
            var f = new PortfoyFirmasiDto { KisaAd = "LIME" };
            for (var a = 1; a <= 12; a++)
                f.Seri.Add(a <= 9
                    ? new PortfoyAyDto { Yil = 2026, Ay = a, Yuklu = true, NetSatis = a * 100m }
                    : new PortfoyAyDto { Yil = 2026, Ay = a, Not = "gelecek ay" });

            var v = PfGrafikVerisi.Cizgi(f, seciliAy: 3);

            Assert.Equal(12, v.Aylar.Count);
            Assert.Equal("Oca 26", v.Aylar[0].Kisa);
            Assert.Equal("Ara 26", v.Aylar[11].Kisa);
            Assert.Equal(2, v.Secili);                              // Mart, son eleman değil
            Assert.All(v.Seriler[0].Degerler.Skip(9), d => Assert.Null(d));
            Assert.Equal("gelecek ay", v.Notlar[11]);
        }

        [Fact]
        public void Cubuk_degeri_olmayan_firmayi_almaz_buyukten_kucuge_toplam_yok()
        {
            var p = new PortfoyDto
            {
                Donem = "2026-09",
                Firmalar = { Firma(1, "A", 500m, null), Firma(2, "B", null, null), Firma(3, "C", 900m, 800m) }
            };

            var v = PfGrafikVerisi.Cubuk(p, 1);

            Assert.Equal(new[] { 3, 1 }, v.Satirlar.Select(r => r.FirmaId));
            Assert.Equal(new[] { 900m, 500m }, v.Satirlar.Select(r => r.Deger));
            Assert.Equal(1, v.SeciliFirmaId);
            Assert.Equal(1, PfGrafikVerisi.CubuktaOlmayan(p));
            Assert.DoesNotContain(typeof(PfCubukVerisi).GetProperties(), pr => pr.Name.Contains("Toplam"));
        }

        [Fact]
        public void Dusen_yukselen_ilk_uc_karsilastirmasi_olmayan_girmez()
        {
            var p = new PortfoyDto
            {
                Firmalar =
                {
                    Firma(1, "A", 50m, 100m),    // −50%
                    Firma(2, "B", 90m, 100m),    // −10%
                    Firma(3, "C", 80m, 100m),    // −20%
                    Firma(4, "D", 70m, 100m),    // −30%
                    Firma(5, "E", 150m, 100m),   // +50%
                    Firma(6, "F", 110m, 100m),   // +10%
                    Firma(7, "G", 100m, null),   // karşılaştırma yok
                    Firma(8, "H", 100m, 0m),     // karşılaştırma sıfır
                    Firma(9, "I", 100m, 100m)    // değişim yok
                }
            };

            var (dusen, yukselen) = PfGrafikVerisi.Hareketler(p);

            Assert.Equal(new[] { "A", "D", "C" }, dusen.Select(h => h.Firma.KisaAd));
            Assert.Equal(new[] { "E", "F" }, yukselen.Select(h => h.Firma.KisaAd));
            Assert.Equal(-0.5m, dusen[0].Oran);
            Assert.DoesNotContain(dusen.Concat(yukselen), h => h.Firma.KisaAd is "G" or "H" or "I");
        }
    }
}
