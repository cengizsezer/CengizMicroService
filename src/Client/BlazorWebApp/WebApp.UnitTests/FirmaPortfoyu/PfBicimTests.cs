using WebApp.Pages.FirmaPortfoyu;

namespace WebApp.UnitTests.FirmaPortfoyu
{
    /// <summary>
    /// Portföy ekranının değişim kuralı: gelir ve kâr artışı yeşil; gider, alacak ve borç artışı kırmızı;
    /// karşılaştırma yoksa ya da sıfırsa "—". Tutar biçimi taslaktaki gibi.
    /// </summary>
    public class PfBicimTests
    {
        [Theory]
        [InlineData(1200, 1000, true, "▲20,0%", PfYon.Iyi)]     // gelir arttı
        [InlineData(800, 1000, true, "▼20,0%", PfYon.Kotu)]     // gelir düştü
        [InlineData(1200, 1000, false, "▲20,0%", PfYon.Kotu)]   // gider arttı
        [InlineData(800, 1000, false, "▼20,0%", PfYon.Iyi)]     // gider düştü
        [InlineData(1002, 1000, true, "≈0,2%", PfYon.Yatay)]
        [InlineData(-50, -100, true, "▲50,0%", PfYon.Iyi)]      // zarar azaldı: kâr arttı
        public void Degisim_yonu_ve_rengi(double bu, double onceki, bool artisIyi, string metin, PfYon yon)
        {
            var (m, y) = PfBicim.Degisim((decimal)bu, (decimal)onceki, artisIyi);
            Assert.Equal(metin, m);
            Assert.Equal(yon, y);
        }

        [Fact]
        public void Karsilastirma_yoksa_ya_da_sifirsa_cizgi()
        {
            Assert.Equal(("—", PfYon.Yatay), PfBicim.Degisim(100m, null, true));
            Assert.Equal(("—", PfYon.Yatay), PfBicim.Degisim(null, 100m, true));
            Assert.Equal(("—", PfYon.Yatay), PfBicim.Degisim(100m, 0m, true));
        }

        [Theory]
        [InlineData(1234567.0, "1,23 Mn ₺")]
        [InlineData(-1234567.0, "−1,23 Mn ₺")]
        [InlineData(12345.0, "12 bin ₺")]
        [InlineData(999.0, "999 ₺")]
        public void Tutar_bicimi(double v, string beklenen) => Assert.Equal(beklenen, PfBicim.Tl((decimal)v));

        [Fact]
        public void Bos_tutar_cizgi_ve_donem_adi()
        {
            Assert.Equal("—", PfBicim.Tl(null));
            Assert.Equal("Eylül 2026", PfBicim.DonemAdi("2026-09"));
            Assert.Equal("2026-09", PfBicim.Donem(2026, 9));
        }
    }
}
