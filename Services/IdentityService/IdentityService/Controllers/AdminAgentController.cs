using IdentityService.Application.Models.Agent;
using IdentityService.Application.Services.Agent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityService.Controllers
{
    /// <summary>
    /// Ajan kayıtlarının yönetimi. Yol <c>api/auth/admin/agents</c>: gateway'in
    /// mevcut <c>/auth/admin/{everything}</c> kuralından geçiyor, o kural da zaten
    /// <c>role: Admin</c> istiyor. Yeni bir gateway satırı gerekmedi.
    /// </summary>
    [Route("api/auth/admin/agents")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminAgentController : ControllerBase
    {
        private readonly IAjanKimlikServisi _ajanlar;

        public AdminAgentController(IAjanKimlikServisi ajanlar) => _ajanlar = ajanlar;

        [HttpGet]
        public async Task<ActionResult<List<AjanListeSatiri>>> Listele(CancellationToken ct)
            => Ok(await _ajanlar.ListeleAsync(ct));

        /// <summary>
        /// Yeni ajan. Yanıttaki ham anahtar <b>bir kez</b> dönüyor: sonraki hiçbir
        /// çağrı onu veremez, çünkü veritabanında yok.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<YeniAjanYaniti>> Olustur(
            [FromBody] YeniAjanIstegi istek, CancellationToken ct)
        {
            if (istek is null || string.IsNullOrWhiteSpace(istek.Ad))
                return BadRequest("Ajan adı zorunlu.");

            var yanit = await _ajanlar.OlusturAsync(istek, KullaniciId(), ct);
            return Ok(yanit);
        }

        [HttpPost("{id:int}/iptal")]
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
