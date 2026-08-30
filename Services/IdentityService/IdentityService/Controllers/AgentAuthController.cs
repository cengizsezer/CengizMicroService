using IdentityService.Application.Models.Agent;
using IdentityService.Application.Services.Agent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Controllers
{
    /// <summary>
    /// Ajanın anahtarını token'a çevirdiği uç.
    ///
    /// Yol <c>api/auth/agent</c> seçildi: gateway'in mevcut <c>/auth/{everything}</c>
    /// kuralı bunu olduğu gibi taşıyor, yani dışarıdan <c>/auth/agent/token</c>
    /// çalışıyor ve ne Ocelot ne nginx yapılandırması değişiyor.
    ///
    /// <b>Anonim ama sınırlı:</b> ajanın elinde token yok, anahtar var — o yüzden
    /// <see cref="AllowAnonymousAttribute"/>. Anahtar deneyerek bulunmasın diye
    /// istek sayısı IP başına sınırlanıyor (<see cref="AjanHizSiniri"/>) ve her
    /// başarısız deneme loglanıyor.
    /// </summary>
    [Route("api/auth/agent")]
    [ApiController]
    [AllowAnonymous]
    public class AgentAuthController : ControllerBase
    {
        private readonly IAjanKimlikServisi _ajanlar;
        private readonly ILogger<AgentAuthController> _log;

        public AgentAuthController(IAjanKimlikServisi ajanlar, ILogger<AgentAuthController> log)
        {
            _ajanlar = ajanlar;
            _log = log;
        }

        [HttpPost("token")]
        [EnableRateLimiting(AjanHizSiniri.Politika)]
        public async Task<ActionResult<AjanTokenYaniti>> Token(
            [FromBody] AjanTokenIstegi istek, CancellationToken ct)
        {
            var yanit = await _ajanlar.TokenUretAsync(istek?.AjanAnahtari ?? string.Empty, ct);

            if (yanit is null)
            {
                // Hangi nedenle reddedildiği dışarı söylenmiyor: "anahtar yok" ile
                // "anahtar iptal" arasındaki fark, deneme yapana bilgi verir.
                _log.LogWarning("Ajan token isteği reddedildi. İstemci: {Ip}",
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor");

                return Unauthorized("Ajan anahtarı geçersiz.");
            }

            return Ok(yanit);
        }
    }
}
