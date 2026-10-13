using CatalogService.Api.Infrastructure.VeriKoruma;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogService.UnitTests.Parolalar
{
    /// <summary>
    /// Data Protection ayarı: Docker'da kalıcı klasör (/var/catalog-dp-keys), yerelde .NET varsayılanı; açılış denetimi
    /// klasör yazılamıyorsa hata verir (sessizce geçici anahtarla çalışmaz) ve anahtarın klasöre düştüğünü doğrular.
    /// </summary>
    public class VeriKorumaAyariTests
    {
        private static IConfiguration Ayar(string? klasor) => new ConfigurationBuilder()
            .AddInMemoryCollection(klasor is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["DataProtection:KeysFolder"] = klasor })
            .Build();

        [Fact]
        public void Klasor_yapilandirmadan_docker_varsayilani_yerelde_net_varsayilani()
        {
            Assert.Equal("/anahtarlar", VeriKorumaAyari.KlasorBul(Ayar(" /anahtarlar "), "Docker"));
            Assert.Equal("/var/catalog-dp-keys", VeriKorumaAyari.KlasorBul(Ayar(null), "Docker"));
            Assert.Null(VeriKorumaAyari.KlasorBul(Ayar(null), "Development"));
        }

        [Fact]
        public void Yazilabilir_klasorde_anahtar_diske_yazilir_ve_yeniden_acilista_ayni_anahtarla_cozulur()
        {
            var klasor = Path.Combine(Path.GetTempPath(), "catalog-dp-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string sifreli;
                using (var sp1 = new ServiceCollection().AddCatalogVeriKoruma(klasor).BuildServiceProvider())
                {
                    VeriKorumaAyari.Denetle(sp1, klasor, NullLogger.Instance);
                    Assert.NotEmpty(Directory.EnumerateFiles(klasor, "key-*.xml"));
                    sifreli = new Api.Features.Parolalar.Services.ParolaSifreleyici(
                        sp1.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>()).Sifrele("gizli");
                }

                // "Yeniden başlatma": yeni sağlayıcı aynı klasörden okur, eski şifre çözülür.
                using var sp2 = new ServiceCollection().AddCatalogVeriKoruma(klasor).BuildServiceProvider();
                var cozucu = new Api.Features.Parolalar.Services.ParolaSifreleyici(
                    sp2.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
                Assert.Equal("gizli", cozucu.Coz(sifreli));
            }
            finally
            {
                if (Directory.Exists(klasor)) Directory.Delete(klasor, recursive: true);
            }
        }

        [Fact]
        public void Klasor_yazilamiyorsa_acilis_denetimi_hata_verir()
        {
            // Klasör yolu aslında bir DOSYA: dizin oluşturulamaz, yazılamaz.
            var dosya = Path.GetTempFileName();
            try
            {
                using var sp = new ServiceCollection().AddCatalogVeriKoruma(dosya).BuildServiceProvider();
                var ex = Assert.Throws<InvalidOperationException>(() => VeriKorumaAyari.Denetle(sp, dosya, NullLogger.Instance));
                Assert.Contains("yazılamıyor", ex.Message);
            }
            finally
            {
                File.Delete(dosya);
            }
        }
    }
}
