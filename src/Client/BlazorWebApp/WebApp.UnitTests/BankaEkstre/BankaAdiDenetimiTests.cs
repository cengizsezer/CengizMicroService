using WebApp.Shared.Dto.BankaEkstre;

namespace WebApp.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka adı biçim uyarısı. Kural yumuşak: kaydı <b>engellemez</b>, yalnız uyarır —
    /// kullanıcı bilerek uzun bir ad yazmış olabilir. Uyarının sebebi, alana tam hesap adı
    /// yazıldığında ("Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı") o metnin hiçbir
    /// ekstre açıklamasında geçmemesi ve eşleşmenin hiç olmaması.
    /// </summary>
    public class BankaAdiDenetimiTests
    {
        [Theory]
        [InlineData("Vakıfbank")]
        [InlineData("İş Bankası")]
        [InlineData("TEB")]
        [InlineData("  Ziraat  ")]
        [InlineData("")]
        [InlineData(null)]
        public void Kisa_banka_adi_uyari_uretmez(string? bankaAdi)
            => Assert.Null(BankaAdiDenetimi.Uyari(bankaAdi));

        [Fact]
        public void Uzun_ad_uyarir()
        {
            var uyari = BankaAdiDenetimi.Uyari(new string('A', BankaAdiDenetimi.EnFazlaUzunluk + 1));

            Assert.NotNull(uyari);
            Assert.Contains(BankaAdiDenetimi.EnFazlaUzunluk.ToString(), uyari);
        }

        [Theory]
        [InlineData("Vakıfbank, Vadeli")]
        [InlineData("Ziraat - Vadesiz")]
        public void Virgul_veya_tire_uyarir(string bankaAdi)
            => Assert.NotNull(BankaAdiDenetimi.Uyari(bankaAdi));

        [Fact]
        public void Hem_uzun_hem_ayracli_ad_tek_uyari_verir()
        {
            var uyari = BankaAdiDenetimi.Uyari("Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı");

            Assert.NotNull(uyari);
            Assert.Contains("kısa banka adı", uyari);
        }
    }
}
