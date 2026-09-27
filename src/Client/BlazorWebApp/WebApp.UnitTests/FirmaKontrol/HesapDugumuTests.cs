using WebApp.Application.Services;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Hesap kırılım ağacının kod çözümlemesi ve doğrulama kuralı.
    ///
    /// EN ÖNEMLİ KURAL: alt kırılımlar hiçbir toplama girmez — 600, 600 1 ve
    /// 600 1 20 + 600 1 21 aynı paradır. Bu testler ağacın yapısını doğrular;
    /// toplam üreten hiçbir yol yoktur.
    /// </summary>
    public class HesapDugumuTests
    {
        private static HesapDugumu Olustur(string kod, decimal bakiye = 0m) =>
            HesapDugumu.Olustur(kod, "test", 0m, 0m, bakiye)!;

        [Theory]
        [InlineData("600", 1, null, "600")]
        [InlineData("600 1", 2, "600", "600")]
        [InlineData("600 1 21", 3, "600 1", "600")]
        [InlineData("120 1 A08", 3, "120 1", "120")]          // parça harf içerebilir
        [InlineData("770 6 255", 3, "770 6", "770")]
        [InlineData("102 1 1 02", 4, "102 1 1", "102")]       // dört seviye
        public void Kodu_cozer_seviye_ust_kod_ve_ana_hesap(
            string kod, int seviye, string? ustKod, string anaHesap)
        {
            var d = Olustur(kod);

            Assert.Equal(seviye, d.Seviye);
            Assert.Equal(ustKod, d.UstKod);
            Assert.Equal(anaHesap, d.AnaHesapKodu);
        }

        [Fact]
        public void Ardisik_bosluklar_tek_ayirici_sayilir()
        {
            var d = Olustur("600   1    21");

            Assert.Equal(3, d.Seviye);
            Assert.Equal("600 1 21", d.Kod);
            Assert.Equal("600 1", d.UstKod);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ABC")]
        [InlineData("60")]        // üç haneli değil
        [InlineData("6000")]      // üç haneli değil
        [InlineData("TOPLAM")]
        public void Ilk_parcasi_uc_haneli_sayi_degilse_dugum_uretmez(string kod)
        {
            Assert.Null(HesapDugumu.Olustur(kod, "x", 0m, 0m, 0m));
        }

        [Fact]
        public void Bakiye_verilmezse_borc_eksi_alacak_kullanilir()
        {
            var d = HesapDugumu.Olustur("600 1", "Yurtiçi Satışlar", borc: 0m, alacak: 21_458_746.99m, bakiye: null)!;

            Assert.Equal(-21_458_746.99m, d.Bakiye);
        }

        /// <summary>
        /// Madde 7: ekrandaki tutar MaliTabloIsareti ile sunum yönüne çevrilir.
        /// 600 alacak bakiyelidir; kullanıcıya pozitif görünmelidir.
        /// </summary>
        [Fact]
        public void Gelir_hesabi_sunum_yonunde_pozitif_gorunur()
        {
            var d = HesapDugumu.Olustur("600 1 21", "Yurtiçi Satışlar", 0m, 9_264_692.00m, null)!;

            var sunum = MaliTabloIsareti.Uygula(d.Bakiye, MaliTabloBolumu.GelirTablosu);

            Assert.Equal(6, d.AnaGrup);
            Assert.Equal(9_264_692.00m, sunum);
        }

        /// <summary>
        /// Madde 5: doğrudan alt düğümlerin toplamı ana hesaba eşit mi (0,01 tolerans).
        /// 600 1 20 + 600 1 21 = 600 1 senaryosu.
        /// </summary>
        [Fact]
        public void Dogrulama_alt_toplam_ana_hesaba_esitse_tutuyor()
        {
            var ana = Olustur("600 1", -21_458_746.99m);
            var cocuklar = new[]
            {
                Olustur("600 1 20", -12_194_054.99m),
                Olustur("600 1 21", -9_264_692.00m),
            };

            var fark = ana.Bakiye - cocuklar.Sum(c => c.Bakiye);

            Assert.True(Math.Abs(fark) <= 0.01m);
        }

        [Fact]
        public void Dogrulama_fark_varsa_tutmuyor_ve_fark_raporlanir()
        {
            var ana = Olustur("770", 2_374_451.51m);
            var cocuklar = new[] { Olustur("770 1", 2_000_000.00m) };

            var fark = ana.Bakiye - cocuklar.Sum(c => c.Bakiye);

            Assert.True(Math.Abs(fark) > 0.01m);
            Assert.Equal(374_451.51m, fark);
        }

        /// <summary>Ağaç yapısı yalnızca UstKod ile kurulur; sabit karakter genişliği yoktur.</summary>
        [Fact]
        public void Agac_ust_kod_ile_turetilir()
        {
            var dugumler = new[]
            {
                Olustur("600"), Olustur("600 1"), Olustur("600 1 20"), Olustur("600 1 21"),
                Olustur("770"), Olustur("770 6 255"),
            };

            var cocuklar = dugumler
                .Where(d => d.UstKod is not null)
                .GroupBy(d => d.UstKod!)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Kod).ToList());

            Assert.Equal(new[] { "600 1" }, cocuklar["600"]);
            Assert.Equal(new[] { "600 1 20", "600 1 21" }, cocuklar["600 1"]);
            Assert.Equal(new[] { "770 6 255" }, cocuklar["770 6"]);   // ara düğüm mizanda yoksa da kod türetilir
            Assert.Equal(2, dugumler.Count(d => d.Seviye == 1));
            Assert.Equal(4, dugumler.Count(d => d.Seviye > 1));
        }
    }
}
