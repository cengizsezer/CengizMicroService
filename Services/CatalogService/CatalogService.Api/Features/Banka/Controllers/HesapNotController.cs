using CatalogService.Api.Features.Banka.Dtos;
using CatalogService.Api.Features.Banka.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Banka.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class HesapNotController : ControllerBase
    {
        private readonly IHesapNotService _service;

        public HesapNotController(IHesapNotService service)
        {
            _service = service;
        }

        // Bir hesabın, bakılan ay (yil/ay) için ilgili notları (Sabit + Genel her ay,
        // Ay/Gun kapsamlılar yalnızca o ay). Sabit olanlar önce.
        [HttpGet("{hesapId:int}")]
        [ProducesResponseType(typeof(List<NotDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<NotDto>>> GetByHesap(
            int hesapId,
            [FromQuery] int yil,
            [FromQuery] int ay,
            CancellationToken ct)
        {
            var list = await _service.GetByHesapAsync(hesapId, yil, ay);
            return Ok(list);
        }

        [HttpPost]
        [ProducesResponseType(typeof(NotDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<NotDto>> Create([FromBody] NotCreateDto dto, CancellationToken ct)
        {
            var created = await _service.CreateAsync(dto);
            if (created is null) return NotFound(new { message = $"Hesap bulunamadı: Id={dto.HesapId}" });
            return CreatedAtAction(nameof(GetByHesap), new { hesapId = created.HesapId }, created);
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }
    }
}
