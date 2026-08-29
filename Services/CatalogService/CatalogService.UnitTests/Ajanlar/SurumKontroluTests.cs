using CatalogService.Api.Features.Ajanlar.Services;

namespace CatalogService.UnitTests.Ajanlar
{
    public class SurumKontroluTests
    {
        [Theory]
        [InlineData("1.0.0", "1.0.0")]   // eşit
        [InlineData("1.2.0", "1.1.9")]   // yeni
        [InlineData("1.10.0", "1.9.0")]  // metin karşılaştırması burada yanılırdı
        [InlineData(" 2.0.0 ", "1.0.0")] // kenar boşluğu
        public void Asgariyi_karsilayan_surum_gecer(string ajan, string asgari)
        {
            Assert.True(SurumKontrolu.Uygun(ajan, asgari, out var mesaj));
            Assert.Equal(string.Empty, mesaj);
        }

        [Theory]
        [InlineData("1.0.0", "1.0.1")]
        [InlineData("1.9.0", "1.10.0")]
        [InlineData("0.9.9", "1.0.0")]
        public void Asgarinin_altindaki_surum_reddedilir(string ajan, string asgari)
        {
            Assert.False(SurumKontrolu.Uygun(ajan, asgari, out var mesaj));
            Assert.Contains(asgari, mesaj);
            Assert.Contains("güncelleyin", mesaj, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("beta")]
        public void Okunamayan_ajan_surumu_reddedilir(string? ajan)
        {
            Assert.False(SurumKontrolu.Uygun(ajan, "1.0.0", out var mesaj));
            Assert.Contains("okunamadı", mesaj);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("en-son")]
        public void Bozuk_asgari_ayar_kimseyi_disarida_birakmaz(string? asgari)
        {
            // Yanlış yazılmış bir yapılandırma satırı bütün ofisi bağlantısız
            // bırakmasın: kontrol atlanır, kayıt kabul edilir.
            Assert.True(SurumKontrolu.Uygun("1.0.0", asgari, out _));
        }
    }
}
