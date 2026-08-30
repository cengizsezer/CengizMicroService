using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;

namespace IdentityService.Application.Services.Agent
{
    /// <summary>
    /// Ajan token ucunun hız sınırı.
    ///
    /// Uç anonim olmak zorunda (ajanın elinde token yok, anahtar var), dolayısıyla
    /// tek koruma anahtarın kendisi. 256 bitlik bir anahtarı denemeyle bulmak zaten
    /// mümkün değil; sınır asıl olarak servisin bir deneme seline hash doğrulaması
    /// yaparak boğulmasını engelliyor — her deneme bir PBKDF2 hesabı demek.
    ///
    /// Pencere IP başına: ofisteki tek ajan dakikada bir kez bile token istemiyor
    /// (token 8 saat yaşıyor), yani gerçek trafiğin sınıra yaklaşma ihtimali yok.
    /// </summary>
    public static class AjanHizSiniri
    {
        public const string Politika = "ajan-token";

        public const int PencereBasinaIstek = 10;
        public static readonly TimeSpan Pencere = TimeSpan.FromMinutes(1);

        public static void Ekle(RateLimiterOptions o)
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.AddPolicy(Politika, http => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen-ip",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PencereBasinaIstek,
                    Window = Pencere,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));

            o.OnRejected = async (ctx, ct) =>
            {
                var log = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(AjanHizSiniri).FullName!);

                log.LogWarning("Hız sınırı: {Ip} adresinden gelen istek reddedildi ({Yol})",
                    ctx.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
                    ctx.HttpContext.Request.Path);

                ctx.HttpContext.Response.Headers.RetryAfter =
                    ((int)Pencere.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                await ctx.HttpContext.Response.WriteAsync("Çok fazla deneme. Biraz sonra tekrar deneyin.", ct);
            };
        }
    }
}
