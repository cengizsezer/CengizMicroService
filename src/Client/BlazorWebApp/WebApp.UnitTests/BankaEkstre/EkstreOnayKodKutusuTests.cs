using System;
using System.IO;
using Xunit;

namespace WebApp.UnitTests.BankaEkstre
{
    /// <summary>
    /// Madde 4: onay ekranında kod kutusu satır bazında olmalı.
    ///
    /// Test kaynak seviyesindedir — proje bileşen render eden bir kütüphane (bUnit)
    /// içermiyor ve yalnız bu iki satır için bağımlılık eklemek istenmedi. Yine de
    /// bildirilen iki nedeni de kapatıyor:
    /// <list type="bullet">
    /// <item>Örnek kod içeren placeholder ("120 D22"), önerisi olmayan satırların hepsinde
    /// aynı gerçek değer varmış gibi okunuyordu.</item>
    /// <item><c>@key</c> yokken satır onaylanıp liste kısalınca Blazor input elemanlarını
    /// konuma göre yeniden kullanıyor, kutudaki değer başka satırdan devralınıyordu.</item>
    /// </list>
    /// </summary>
    public class EkstreOnayKodKutusuTests
    {
        private static string Kaynak()
        {
            var dizin = AppContext.BaseDirectory;

            while (dizin is not null)
            {
                var yol = Path.Combine(dizin, "WebApp", "Pages", "BankaEkstre", "EkstreOnayPage.razor");
                if (File.Exists(yol)) return File.ReadAllText(yol);

                dizin = Path.GetDirectoryName(dizin);
            }

            throw new FileNotFoundException("EkstreOnayPage.razor bulunamadı.");
        }

        [Fact]
        public void Kod_kutusunda_ornek_kod_placeholderi_yok()
        {
            Assert.DoesNotContain("placeholder=\"120 D22\"", Kaynak(), StringComparison.Ordinal);
        }

        [Fact]
        public void Bekleyen_satirlar_key_ile_kimliklendirilir()
        {
            var kaynak = Kaynak();

            Assert.Contains("@key=\"satir.Id\"", kaynak, StringComparison.Ordinal);
        }

        [Fact]
        public void Kutunun_degeri_satir_bazinda_okunur()
        {
            // Değer satır Id'siyle anahtarlanmış sözlükten gelir; öneri yoksa boş string.
            var kaynak = Kaynak();

            Assert.Contains("value=\"@Kod(satir.Id)\"", kaynak, StringComparison.Ordinal);
            Assert.Contains(
                "_kodlar[satir.Id] = satir.OnaylananHesapKodu ?? satir.OnerilenHesapKodu ?? string.Empty;",
                kaynak,
                StringComparison.Ordinal);
        }
    }
}
