using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Parolalar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace CatalogService.Api.Features.Parolalar
{
    /// <summary>
    /// Parolalar uçlarının sunucu yetkisi: insan token'ı + <c>pkf</c> rolü (sayfanın <c>[Authorize(Roles = "pkf")]</c>'ıyla
    /// aynı kural; yalnız sayfada olsaydı alan ucu doğrudan çağrılıp şifre okunabilirdi).
    ///
    /// Rol claim'i: IdentityService her rolü iki kez basar — <c>ClaimTypes.Role</c> ve kısa <c>"role"</c>. Catalog'da
    /// JwtBearer kısa <c>"role"</c>'ü <c>ClaimTypes.Role</c>'e çevirir (MapInboundClaims varsayılanı), TokenValidationParameters'ın
    /// RoleClaimType'ı da <c>ClaimTypes.Role</c>; <c>RequireRole("pkf")</c> bu yüzden ek eşleme olmadan çalışır.
    /// Roller kiracı üyeliğine göre basılır (giriş yapılan tenant'taki roller).
    /// </summary>
    public static class ParolaYetkisi
    {
        public const string Politika = "ParolaYonetimi";
        public const string Rol = "pkf";

        public static void Ekle(AuthorizationOptions o)
            => o.AddPolicy(Politika, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => !AjanKimligi.AjanMi(ctx.User))   // ajan token'ı asla
                .RequireRole(Rol));

        /// <summary>Uç bu politikayı mı taşıyor (Parolalar controller'ı)?</summary>
        public static bool ParolaUcuMu(Endpoint? uc)
            => uc?.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == Politika) == true;
    }

    /// <summary>
    /// Yetkilendirme sonucunu varsayılan işleyiciye bırakmadan önce: Parolalar ucunda 403 (kimliği doğrulanmış ama
    /// yetkisiz) ise erişim kaydına "reddedildi" yazar — şifre ya da gövde yok, yalnız id / alan / kullanıcı. Diğer
    /// bütün uçlarda ve 401'de davranış aynen varsayılan.
    /// </summary>
    public sealed class ParolaReddiKaydedici : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _varsayilan = new();
        private readonly ILogger<ParolaReddiKaydedici> _logger;

        public ParolaReddiKaydedici(ILogger<ParolaReddiKaydedici> logger) => _logger = logger;

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
                                      PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Forbidden && ParolaYetkisi.ParolaUcuMu(context.GetEndpoint()))
            {
                try
                {
                    var rota = context.Request.RouteValues;
                    var id = int.TryParse(rota["id"]?.ToString(), out var i) ? i : (int?)null;
                    var servis = context.RequestServices.GetRequiredService<IParolaService>();
                    await servis.ReddiKaydetAsync(id, rota["alan"]?.ToString(), context.RequestAborted);
                }
                catch (Exception ex)
                {
                    // Kayıt yazılamasa da 403 döner. Mesajda istek içeriği yok.
                    _logger.LogWarning(ex, "Parolalar: reddedilen erişim kaydedilemedi.");
                }
            }

            await _varsayilan.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
