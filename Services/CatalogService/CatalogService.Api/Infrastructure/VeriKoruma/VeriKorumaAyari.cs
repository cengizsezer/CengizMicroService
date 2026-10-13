using Microsoft.AspNetCore.DataProtection;

namespace CatalogService.Api.Infrastructure.VeriKoruma
{
    /// <summary>
    /// ASP.NET Core Data Protection ayarı — Yönetim › Parolalar'daki şifreler bununla şifrelenir.
    ///
    /// Anahtarlar kaybolursa kayıtlı şifreler BİR DAHA ÇÖZÜLEMEZ. Bu yüzden:
    ///   • Docker'da anahtar klasörü kalıcı volume'dür (docker-compose.override.yml: catalog_dp_keys → /var/catalog-dp-keys).
    ///     Yol <c>DataProtection:KeysFolder</c> yapılandırmasından gelir; Docker'da / konteynerde boşsa /var/catalog-dp-keys.
    ///   • Yerelde (Docker dışı) klasör verilmezse .NET'in varsayılanı kullanılır (Windows: %LOCALAPPDATA%\ASP.NET\
    ///     DataProtection-Keys, DPAPI ile korunur) — repo dışı. Repo içine yol VERİLMEZ (dp-keys/ Sovos'un, ayrı iş).
    ///   • Açılışta klasör yazılamıyorsa ya da anahtar diske düşmüyorsa uygulama açık bir hatayla DURUR; sessizce
    ///     geçici (bellekteki) anahtarla çalışmaz.
    /// Uygulama adı sabit "CatalogService": anahtar halkası bu adla ayrılır (Sovos'un anahtarlarıyla paylaşılmaz).
    /// </summary>
    public static class VeriKorumaAyari
    {
        public const string UygulamaAdi = "CatalogService";
        public const string DockerVarsayilanKlasor = "/var/catalog-dp-keys";

        /// <summary>Kullanılacak anahtar klasörü; null = .NET varsayılanı (yalnız Docker dışı).</summary>
        public static string? KlasorBul(IConfiguration configuration, string ortam)
        {
            var klasor = configuration["DataProtection:KeysFolder"];
            if (!string.IsNullOrWhiteSpace(klasor)) return klasor.Trim();
            // Konteynerde .NET varsayılanı (~/.aspnet) konteynerle birlikte silinir: ortam adı ne olursa olsun volume yolu.
            var konteyner = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true",
                                          StringComparison.OrdinalIgnoreCase);
            return konteyner || string.Equals(ortam, "Docker", StringComparison.OrdinalIgnoreCase) ? DockerVarsayilanKlasor : null;
        }

        public static IServiceCollection AddCatalogVeriKoruma(this IServiceCollection services, string? klasor)
        {
            var dp = services.AddDataProtection().SetApplicationName(UygulamaAdi);
            if (klasor is not null) dp.PersistKeysToFileSystem(new DirectoryInfo(klasor));
            return services;
        }

        /// <summary>
        /// Açılış denetimi: klasör var ve yazılabilir mi; bir şifreleme turu anahtarı diske yazıyor mu. Başarısızsa
        /// <see cref="InvalidOperationException"/> (çağıran Fatal loglar ve uygulama açılmaz).
        /// </summary>
        public static void Denetle(IServiceProvider services, string? klasor, ILogger logger)
        {
            if (klasor is not null)
            {
                try
                {
                    Directory.CreateDirectory(klasor);
                    var deneme = Path.Combine(klasor, $".yazma-denemesi-{Guid.NewGuid():N}");
                    File.WriteAllText(deneme, "x");
                    File.Delete(deneme);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Data Protection anahtar klasörü yazılamıyor: '{klasor}'. Parolalar şifrelenemez; geçici anahtarla " +
                        "çalışılmayacak. Docker'da catalog_dp_keys volume'ünün bağlı ve uygulama kullanıcısına ($APP_UID) " +
                        "yazılabilir olduğunu kontrol edin.", ex);
                }
            }

            // Bir tur şifrele/çöz: anahtar yoksa şimdi üretilir ve klasöre yazılır.
            var koruyucu = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("CatalogService.AcilisDenetimi");
            if (koruyucu.Unprotect(koruyucu.Protect("denetim")) != "denetim")
                throw new InvalidOperationException("Data Protection şifrele/çöz denetimi başarısız.");

            if (klasor is not null && !Directory.EnumerateFiles(klasor, "key-*.xml").Any())
                throw new InvalidOperationException(
                    $"Data Protection anahtarı '{klasor}' klasörüne yazılmadı; anahtar kalıcı değil. Parolalar şifrelenemez.");

            logger.LogInformation("Data Protection hazır: anahtar klasörü {Klasor}", klasor ?? "(.NET varsayılanı, repo dışı)");
        }
    }
}
