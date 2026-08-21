using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Öğrenilen eşleşmeler. Yanlış onaylanan bir eşleşme bir daha sorulmadan tekrarlanır;
    /// bu yüzden liste görülebilir, düzeltilebilir ve silinebilir olmak zorunda.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/eslesmeler")]
    [Authorize]
    public class HesapEslesmeleriController : ControllerBase
    {
        private readonly IHesapEslesmeService _service;

        public HesapEslesmeleriController(IHesapEslesmeService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<HesapEslesmesiDto>>> Ara([FromQuery] string? q,
                                                                    [FromQuery] int enFazla = 100,
                                                                    CancellationToken ct = default)
            => Ok(await _service.AraAsync(q, enFazla, ct));

        [HttpPut("{id:int}")]
        public async Task<ActionResult<HesapEslesmesiDto>> Guncelle(int id, [FromBody] HesapEslesmesiYazDto dto,
                                                                   CancellationToken ct = default)
        {
            try
            {
                var kayit = await _service.GuncelleAsync(id, dto, ct);
                return kayit is null ? NotFound() : Ok(kayit);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Sil(int id, CancellationToken ct)
            => await _service.SilAsync(id, ct) ? NoContent() : NotFound();
    }
}
