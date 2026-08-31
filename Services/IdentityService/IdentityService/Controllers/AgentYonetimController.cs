using IdentityService.Application.Models.Agent;
using IdentityService.Application.Services.Agent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityService.Controllers
{
    /// <summary>
    /// Ajan kayıtlarının yönetimi. Yol <c>api/auth/agents</c>: gateway'in var olan
    /// <c>/auth/{everything}</c> kuralından geçiyor, yeni bir gateway satırı
    /// gerekmedi.
    ///
    /// <b>Neden <c>/auth/admin/</c> altında değil:</b> gateway'in
    /// <c>/auth/admin/{everything}</c> kuralı yolun kendisine
    /// <c>RouteClaimsRequirement: role=Admin</c> koyuyor. Uç orada kaldığı sürece
    /// buradaki attribute ne derse desin, Admin rolü olmayan kullanıcı gateway'i
    /// geçemezdi (bkz. KARARLAR §131).
    ///
    /// Tekil <c>api/auth/agent</c> ile karıştırılmasın: orası ajanın kendi token
    /// ucu ve anonim.
    /// </summary>
    [Route("api/auth/agents")]
    [ApiController]
    [Authorize(Policy = AjanYetkileri.GoruntulePolitikasi)]
    public class AgentYonetimController : ControllerBase
    {
        private readonly IAjanKimlikServisi _ajanlar;

        public AgentYonetimController(IAjanKimlikServisi ajanlar) => _ajanlar = ajanlar;

        [HttpGet]
        public async Task<ActionResult<List<AjanListeSatiri>>> Listele(CancellationToken ct)
            => Ok(await _ajanlar.ListeleAsync(ct));

        /// <summary>
        /// Yeni ajan. Yanıttaki ham anahtar <b>bir kez</b> dönüyor: sonraki hiçbir
        /// çağrı onu veremez, çünkü veritabanında yok.
        /// </summary>
        [HttpPost]
        [Authorize(Policy = AjanYetkileri.DuzenlePolitikasi)]
        public async Task<ActionResult<YeniAjanYaniti>> Olustur(
            [FromBody] YeniAjanIstegi istek, CancellationToken ct)
        {
            if (istek is null || string.IsNullOrWhiteSpace(istek.Ad))
                return BadRequest("Ajan adı zorunlu.");

            var yanit = await _ajanlar.OlusturAsync(istek, KullaniciId(), ct);
            return Ok(yanit);
        }

        [HttpPost("{id:int}/iptal")]
        [Authorize(Policy = AjanYetkileri.DuzenlePolitikasi)]
        public async Task<IActionResult> IptalEt(int id, [FromBody] AjanIptalIstegi istek, CancellationToken ct)
        {
            if (istek is null || string.IsNullOrWhiteSpace(istek.Neden))
                return BadRequest("İptal nedeni zorunlu.");

            return await _ajanlar.IptalEtAsync(id, istek.Neden, ct)
                ? NoContent()
                : NotFound();
        }

        private int KullaniciId()
        {
            var ham = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            return int.TryParse(ham, out var id) ? id : 0;
        }
    }
}
