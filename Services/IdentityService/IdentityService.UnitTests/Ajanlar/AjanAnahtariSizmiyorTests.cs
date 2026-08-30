using IdentityService.Application.Models.Agent;
using IdentityService.Application.Services.Agent;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdentityService.UnitTests.Ajanlar
{
    /// <summary>
    /// "Ham anahtar hiçbir yerde saklanmıyor ve loglanmıyor" kuralının açık
    /// doğrulaması. Bu modülün tek gerekçesi, ofisteki makinede duran sırrın
    /// sunucu tarafında da bir kopyasının bulunmaması: kaybolan anahtar geri
    /// getirilemez, sızan anahtar log dosyalarından okunamaz.
    /// </summary>
    public class AjanAnahtariSizmiyorTests
    {
        [Fact]
        public async Task Ham_anahtar_hicbir_log_satirina_girmiyor()
        {
            var kayitci = new KayitTutanLogger();
            using var db = AjanTestKurulumu.Db();
            var saat = new SahteSaat();
            var servis = new AjanKimlikServisi(
                db, new PasswordHasher<Ajan>(), AjanTestKurulumu.Ayarlar(), saat, kayitci);

            var yeni = await servis.OlusturAsync(new YeniAjanIstegi { Ad = "Ofis Banka PC" }, 1);

            // Anahtarın yaşadığı bütün yollar: kabul, ret, iptal, iptal sonrası ret.
            await servis.TokenUretAsync(yeni.Anahtar);
            await servis.TokenUretAsync(AjanAnahtari.Uret());
            await servis.TokenUretAsync(yeni.Anahtar[..AjanAnahtari.OnEkUzunlugu] + "yanlisgovde");
            await servis.IptalEtAsync(yeni.Id, "Deneme");
            await servis.TokenUretAsync(yeni.Anahtar);

            var govde = yeni.Anahtar[AjanAnahtari.OnEkUzunlugu..];
            Assert.NotEmpty(kayitci.Satirlar);
            Assert.All(kayitci.Satirlar, s => Assert.DoesNotContain(govde, s, StringComparison.Ordinal));
            Assert.All(kayitci.Satirlar, s => Assert.DoesNotContain(yeni.Anahtar, s, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Anahtarin_hash_i_de_log_satirina_girmiyor()
        {
            var kayitci = new KayitTutanLogger();
            using var db = AjanTestKurulumu.Db();
            var servis = new AjanKimlikServisi(
                db, new PasswordHasher<Ajan>(), AjanTestKurulumu.Ayarlar(), new SahteSaat(), kayitci);

            var yeni = await servis.OlusturAsync(new YeniAjanIstegi { Ad = "Ofis Banka PC" }, 1);
            await servis.TokenUretAsync(yeni.Anahtar);

            var hash = (await db.Ajanlar.SingleAsync()).AnahtarHash;
            Assert.All(kayitci.Satirlar, s => Assert.DoesNotContain(hash, s, StringComparison.Ordinal));
        }

        /// <summary>Yazılan bütün log satırlarını biriktiren logger.</summary>
        private sealed class KayitTutanLogger : ILogger<AjanKimlikServisi>
        {
            public List<string> Satirlar { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Satirlar.Add(formatter(state, exception));

                // Yapılandırılmış alanlar da sızma yolu: Serilog bunları ayrı ayrı
                // yazıyor, biçimlenmiş metne bakmak yetmez.
                if (state is IEnumerable<KeyValuePair<string, object?>> alanlar)
                    foreach (var alan in alanlar)
                        Satirlar.Add(alan.Value?.ToString() ?? "");
            }
        }
    }
}
