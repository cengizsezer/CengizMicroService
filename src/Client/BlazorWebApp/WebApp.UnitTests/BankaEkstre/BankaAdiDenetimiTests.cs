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

        // ---- Yeni yazım uyarısı (otomatik tamamlamanın ikinci yarısı) ----
        //
        // "Aynı banka önceliği" kuralı BankaAdi üzerinden çalışır: aynı banka iki yazımla
        // girilirse sistem onları ayrı bankalar sanır ve bankalar arası eşleştirme bozulur.

        private static readonly string[] Mevcut = { "İş Bankası", "Ziraat", "Vakıfbank" };

        [Fact]
        public void Mevcut_bir_ad_uyari_vermez()
            => Assert.Null(BankaAdiDenetimi.YeniBankaUyarisi("Ziraat", Mevcut));

        [Theory]
        [InlineData("ZIRAAT")]
        [InlineData("ziraat")]
        [InlineData("  Vakıfbank  ")]
        public void Buyuk_kucuk_harf_ve_bosluk_ayni_banka_sayilir(string ad)
            => Assert.Null(BankaAdiDenetimi.YeniBankaUyarisi(ad, Mevcut));

        [Fact]
        public void Listede_olmayan_yazim_uyarir()
        {
            var uyari = BankaAdiDenetimi.YeniBankaUyarisi("Ziraat Bankası", Mevcut);

            Assert.NotNull(uyari);
            Assert.Contains("yeni bir banka sekmesi", uyari);
        }

        /// <summary>
        /// Türkçe 'ı'/'I' ordinal karşılaştırmada eşleşmez — sekme şeridi de aynı
        /// karşılaştırmayı kullandığı için "İŞ BANKASI" gerçekten ikinci bir sekme açar.
        /// Uyarının çıkması doğru davranış: kullanıcının düzeltmesi gereken şey tam da bu
        /// (9 sekme, 8 banka).
        /// </summary>
        [Fact]
        public void Turkce_buyuk_i_farki_ayri_banka_sayilir_ve_uyarir()
        {
            var uyari = BankaAdiDenetimi.YeniBankaUyarisi("İŞ BANKASI", Mevcut);

            Assert.NotNull(uyari);

            // Uyarı ile sekme şeridi aynı şeyi söylemeli; ikisi de OrdinalIgnoreCase.
            var sekmeler = Mevcut.Append("İŞ BANKASI").Distinct(StringComparer.OrdinalIgnoreCase).Count();
            Assert.Equal(Mevcut.Length + 1, sekmeler);
        }

        [Fact]
        public void Bos_ad_uyari_vermez()
        {
            Assert.Null(BankaAdiDenetimi.YeniBankaUyarisi(null, Mevcut));
            Assert.Null(BankaAdiDenetimi.YeniBankaUyarisi("   ", Mevcut));
        }

        [Fact]
        public void Hic_hesap_yokken_ilk_banka_da_uyarir()
        {
            // İlk hesap gerçekten yeni bir sekme açar; uyarı doğru ve engellemiyor.
            Assert.NotNull(BankaAdiDenetimi.YeniBankaUyarisi("Vakıfbank", Array.Empty<string>()));
        }
    }
}
