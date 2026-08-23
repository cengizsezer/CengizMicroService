using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Sabit kural tablosu (Katman 4): işlem tipi veya açıklama → hesap kodu.
    /// Mimari hedef "yeni banka = yeni parser + yeni kural satırları"; tablo bu yüzden
    /// Tanımlar ekranından düzenlenir, kod değişmez.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/sabit-kurallar")]
    [Authorize]
    public class SabitKurallarController : ControllerBase
    {
        private readonly ISabitKuralService _service;

        public SabitKurallarController(ISabitKuralService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<SabitKuralDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));

        [HttpPost]
        public async Task<ActionResult<SabitKuralDto>> Create([FromBody] SabitKuralYazDto dto, CancellationToken ct)
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
        public async Task<ActionResult<SabitKuralDto>> Update(int id, [FromBody] SabitKuralYazDto dto, CancellationToken ct)
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
