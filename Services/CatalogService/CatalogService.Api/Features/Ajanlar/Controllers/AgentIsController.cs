using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CatalogService.Api.Features.Ajanlar.Controllers
{
    /// <summary>
    /// Ajan işlerinin insan tarafı: oluştur, izle, iptal et.
    ///
    /// Ajanın kendisi bu uçları kullanmıyor — o hub üzerinden konuşuyor. Politika
    /// bu yüzden <see cref="AjanPolitikalari.YalnizInsan"/>.
    ///
    /// Yol <c>api/catalog/agent</c> altında: gateway'in <c>/catalog/{everything}</c>
    /// kuralı olduğu gibi taşıyor, yeni bir Ocelot satırı gerekmedi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/agent")]
    [Authorize(Policy = AjanPolitikalari.YalnizInsan)]
    public class AgentIsController : ControllerBase
    {
        private readonly IAjanIsServisi _isler;

        public AgentIsController(IAjanIsServisi isler) => _isler = isler;

        /// <summary>
        /// İş oluşturur ve ajan bağlıysa gönderir. Ajan meşgulse <b>409</b> döner ve
        /// çalışan işi bildirir.
        /// </summary>
        [HttpPost("is")]
        public async Task<ActionResult<AjanIsiOlusturSonucuDto>> Olustur(
            [FromBody] YeniAjanIsiDto istek, CancellationToken ct)
        {
            if (istek is null) return BadRequest(new { message = "İstek gövdesi boş." });

            var sonuc = await _isler.OlusturAsync(istek, KullaniciId(), ct);

            if (sonuc.CakisanIs is not null) return Conflict(sonuc);
            if (sonuc.Is is null) return BadRequest(sonuc);

            return Ok(sonuc);
        }

        [HttpGet("is/{id:guid}")]
        public async Task<ActionResult<AjanIsDto>> Getir(Guid id, CancellationToken ct)
        {
            var dto = await _isler.GetirAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        /// <summary>
        /// İş listesi. <c>firmaId</c> verilmezse tüm firmalar döner — okuma
        /// kapsamsız olabilir (KARARLAR §99).
        /// </summary>
        [HttpGet("isler")]
        public async Task<ActionResult<List<AjanIsDto>>> Listele(
            [FromQuery] int? firmaId,
            [FromQuery] AjanIsDurumu? durum,
            [FromQuery] string? ajanId,
            [FromQuery] int enFazla = 50,
            CancellationToken ct = default)
            => Ok(await _isler.ListeleAsync(firmaId, durum, ajanId, enFazla, ct));

        [HttpPost("is/{id:guid}/iptal")]
        public async Task<ActionResult<AjanIsDto>> IptalEt(Guid id, CancellationToken ct)
        {
            var dto = await _isler.IptalAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        private string KullaniciId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;
    }
}
