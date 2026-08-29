using CatalogService.Api.Features.BankaEkstre.Services.Parsing;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Üç yeni ayrıştırıcının ortak hücre okuma kuralları. Bankalar arasında ayrışmasın
    /// diye tek yerde duruyor; buradaki bir hata üç dosyayı birden bozar.
    /// </summary>
    public class TabloDegerTests
    {
        private static TabloHucresi Metin(string metin) => new(metin, null, null);
        private static TabloHucresi Sayi(double sayi) => new(sayi.ToString(System.Globalization.CultureInfo.InvariantCulture), sayi, null);

        [Theory]
        // İş Bankası: saat tireyle ayrılmış.
        [InlineData("26/08/2026-14:58:47", 2026, 8, 26)]
        // Akbank / Ziraat: noktalı tarih.
        [InlineData("27.08.2026", 2026, 8, 27)]
        // Saat boşlukla ayrılmışsa da tarih kısmı alınır.
        [InlineData("26.08.2026 14:58", 2026, 8, 26)]
        // Tire ayraçlı ISO tarih bölünmemeli.
        [InlineData("2026-08-26", 2026, 8, 26)]
        public void Metin_tarihleri_okunur(string ham, int yil, int ay, int gun)
        {
            Assert.True(TabloDeger.Tarih(Metin(ham), out var tarih));
            Assert.Equal(new DateTime(yil, ay, gun), tarih);
        }

        [Fact]
        public void Excel_seri_numarasi_tarihe_cevrilir()
        {
            // Ham XML yolunda hücrenin tarih biçimli olduğu bilinemiyor (Ziraat'in bozuk
            // styles.xml'i yüzünden zaten oraya düşülüyor); makul aralıktaki sayı tarih sayılır.
            var seri = new DateTime(2026, 8, 26).ToOADate();

            Assert.True(TabloDeger.Tarih(Sayi(seri), out var tarih));
            Assert.Equal(new DateTime(2026, 8, 26), tarih);
        }

        [Fact]
        public void Tutar_buyuklugundeki_sayi_tarih_sayilmaz()
        {
            // 92.919,40 TL'lik bir tutar seri numarası aralığının dışında; tarih sanılsaydı
            // bankanın tutar kolonu tarih kolonuna karıştığında satır sessizce kabul edilirdi.
            Assert.False(TabloDeger.Tarih(Sayi(92919.40), out _));
        }

        [Fact]
        public void Sayisal_hucre_metne_cevrilmeden_okunur()
        {
            // Metne çevrilip tr-TR ile ayrıştırılsaydı "12500.75" değeri 1250075 olurdu.
            Assert.True(TabloDeger.Tutar(Sayi(12500.75), out var tutar));
            Assert.Equal(12500.75m, tutar);
        }

        [Theory]
        [InlineData("1.234,56", 1234.56)]
        [InlineData("-12.500,75 TL", -12500.75)]
        [InlineData("1,234.56", 1234.56)]
        public void Metin_tutarlari_iki_kulturde_de_okunur(string ham, double beklenen)
        {
            Assert.True(TabloDeger.Tutar(Metin(ham), out var tutar));
            Assert.Equal((decimal)beklenen, tutar);
        }

        [Fact]
        public void Bos_hucre_tarih_ve_tutar_vermez()
        {
            Assert.False(TabloDeger.Tarih(TabloHucresi.Bos, out _));
            Assert.False(TabloDeger.Tutar(TabloHucresi.Bos, out _));
        }

        [Fact]
        public void Tanimsiz_dosya_bicimi_anlasilir_hata_verir()
        {
            using var akis = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D });   // "%PDF-"

            var hata = Assert.Throws<InvalidDataException>(() => EkstreTabloOkuyucu.Oku(akis, new EkstreParseSonuc()));

            Assert.Contains("tanınmadı", hata.Message);
        }
    }
}
