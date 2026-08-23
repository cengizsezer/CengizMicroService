using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Muhasebe açıklaması şablonları. Banka bazlı; boş ayrıştırıcı tüm bankalarda geçerli.
    /// Yer tutucu listesi de buradan verilir ki ekran ile üretici aynı kaynağı kullansın.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/aciklama-sablonlari")]
    [Authorize]
    public class AciklamaSablonlariController : ControllerBase
    {
        private readonly IAciklamaSablonuService _service;

        public AciklamaSablonlariController(IAciklamaSablonuService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<AciklamaSablonuDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));

        /// <summary>Şablonda kullanılabilecek yer tutucular; ekranda liste olarak gösterilir.</summary>
        [HttpGet("yer-tutucular")]
        public ActionResult<List<YerTutucuDto>> YerTutucular() => Ok(_service.YerTutucular());

        [HttpPost]
        public async Task<ActionResult<AciklamaSablonuDto>> Create([FromBody] AciklamaSablonuYazDto dto, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.CreateAsync(dto, ct));
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<AciklamaSablonuDto>> Update(int id, [FromBody] AciklamaSablonuYazDto dto, CancellationToken ct)
        {
            try
            {
                var kayit = await _service.UpdateAsync(id, dto, ct);
                return kayit is null ? NotFound() : Ok(kayit);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }
}
