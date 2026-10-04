using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CatalogService.Api.Features.Payroll.Acik
{
    /// <summary>
    /// Açık (girişsiz) bordro uçlarının hız sınırı — KARARLAR §155.
    ///
    /// Amaç kötüye kullanımı kesmek, normal kullanımı yavaşlatmak DEĞİL: sınır geniş
    /// (varsayılan dakikada 60) ve koda gömülü değil, <c>BordroAcik:DakikadaIstek</c>
    /// ayarından okunuyor (ortam değişkeni: <c>BordroAcik__DakikadaIstek</c>). Ofis
    /// takılırsa yeniden derlemeden, ayar + yeniden başlatma ile açılır.
    ///
    /// <b>Gerçek istemci IP'si.</b> İstek tarayıcı → nginx → Ocelot → bu servis yolunu
    /// izliyor; <c>RemoteIpAddress</c> burada gateway konteynerinin iç adresi. Ona göre
    /// bölünseydi sınır bütün internet için TEK kova olurdu. nginx gerçek adresi
    /// <c>X-Real-IP</c> (üzerine yazar) ve <c>X-Forwarded-For</c> (sona ekler) başlıklarına
    /// koyuyor, Ocelot ikisini de olduğu gibi aktarıyor. Servis genelinde
    /// <c>UseForwardedHeaders</c> YOK ve bilinçli olarak eklenmedi — o, bütün servisin
    /// <c>RemoteIpAddress</c>'ini değiştirirdi; başlıklar yalnız bu sınırın anahtarı için
    /// okunuyor (<see cref="IstemciAnahtari"/>).
    ///
    /// <b>İki politika, iki ayrı kova.</b> <see cref="Politika"/> hesap uçları içindir
    /// (law-types, calculate, compare-distribution — sonuncusu pencere açılınca ve liste
    /// değişince kendiliğinden çağrılıyor, çalışanlar takılmasın diye burada).
    /// <see cref="DosyaPolitika"/> yalnız dört dosya ucu içindir (Excel/PDF üretimi
    /// hesaptan belirgin şekilde ağır, tıklamayla çalışıyor): varsayılan dakikada 30,
    /// <c>BordroAcik:DosyaDakikadaIstek</c> ayarından. Aksiyon üzerindeki
    /// <c>[EnableRateLimiting]</c> controller'dakini ezer; dosya isteği hesap kovasından
    /// yemez, hesap isteği dosya kovasından yemez.
    /// </summary>
    public static class BordroAcikHizSiniri
    {
        public const string Politika = "bordro-acik";
        public const string AyarAnahtari = "BordroAcik:DakikadaIstek";
        public const int VarsayilanDakikadaIstek = 60;

        public const string DosyaPolitika = "bordro-acik-dosya";
        public const string DosyaAyarAnahtari = "BordroAcik:DosyaDakikadaIstek";
        public const int VarsayilanDosyaDakikadaIstek = 30;

        public static readonly TimeSpan Pencere = TimeSpan.FromMinutes(1);

        public const string RetMesaji =
            "Bordro hesaplayıcısına kısa sürede çok fazla istek gönderildi. " +
            "Lütfen bir dakika bekleyip tekrar deneyin.";

        public const string DosyaRetMesaji =
            "Kısa sürede çok fazla dosya isteği (Excel/PDF) gönderildi. " +
            "Lütfen bir dakika bekleyip tekrar deneyin.";

        public static void Ekle(RateLimiterOptions o, IConfiguration configuration)
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            PolitikaEkle(o, Politika, Sinir(configuration, AyarAnahtari, VarsayilanDakikadaIstek));
            PolitikaEkle(o, DosyaPolitika, Sinir(configuration, DosyaAyarAnahtari, VarsayilanDosyaDakikadaIstek));

            o.OnRejected = async (ctx, ct) =>
            {
                var http = ctx.HttpContext;
                http.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(BordroAcikHizSiniri).FullName!)
                    .LogWarning("Açık bordro hız sınırı: {Ip} reddedildi ({Yol})",
                        IstemciAnahtari(http), http.Request.Path);

                // Hangi politikaya takıldığı uç meta verisinden: dosya ucuysa mesaj ona göre.
                var politika = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

                http.Response.Headers.RetryAfter =
                    ((int)Pencere.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                http.Response.ContentType = "text/plain; charset=utf-8";
                await http.Response.WriteAsync(politika == DosyaPolitika ? DosyaRetMesaji : RetMesaji, ct);
            };
        }

        private static int Sinir(IConfiguration configuration, string anahtar, int varsayilan)
        {
            var deger = configuration.GetValue<int?>(anahtar) ?? varsayilan;
            return deger > 0 ? deger : varsayilan;
        }

        private static void PolitikaEkle(RateLimiterOptions o, string politika, int dakikadaIstek)
            => o.AddPolicy(politika, http => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: IstemciAnahtari(http),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = dakikadaIstek,
                    Window = Pencere,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

        /// <summary>
        /// Sınırın bölündüğü anahtar: isteği atan gerçek istemcinin IP'si.
        ///
        /// Başlıklara YALNIZ doğrudan karşıdaki adres iç ağdaysa (loopback / özel aralık —
        /// yani gateway ya da nginx) güvenilir. Dışarıdan doğrudan gelen bir istek
        /// başlık uydurarak kendine yeni kova açamaz; onun anahtarı kendi adresidir.
        /// Sıra: <c>X-Real-IP</c> (nginx üzerine yazıyor, istemci değiştiremez) →
        /// <c>X-Forwarded-For</c>'un SON girdisi (nginx'in eklediği; öncekiler istemcinin
        /// yazdığı olabilir) → doğrudan karşıdaki adres.
        /// </summary>
        public static string IstemciAnahtari(HttpContext http)
        {
            var karsi = http.Connection.RemoteIpAddress;

            if (karsi is null || IcAgMi(karsi))
            {
                if (IpCoz(http.Request.Headers["X-Real-IP"].ToString()) is { } gercek)
                    return gercek.ToString();

                var xff = http.Request.Headers["X-Forwarded-For"].ToString();
                if (!string.IsNullOrWhiteSpace(xff))
                {
                    var son = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                 .LastOrDefault();
                    if (IpCoz(son) is { } ip)
                        return ip.ToString();
                }
            }

            return karsi is null ? "bilinmeyen-ip" : Normallestir(karsi).ToString();
        }

        private static IPAddress? IpCoz(string? deger)
            => !string.IsNullOrWhiteSpace(deger) && IPAddress.TryParse(deger.Trim(), out var ip)
                ? Normallestir(ip)
                : null;

        private static IPAddress Normallestir(IPAddress ip)
            => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

        public static bool IcAgMi(IPAddress ip)
        {
            ip = Normallestir(ip);
            if (IPAddress.IsLoopback(ip)) return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                return b[0] == 10
                       || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                       || (b[0] == 192 && b[1] == 168);
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var b = ip.GetAddressBytes();
                return (b[0] & 0xFE) == 0xFC   // fc00::/7 (unique local)
                       || ip.IsIPv6LinkLocal;
            }

            return false;
        }
    }
}
