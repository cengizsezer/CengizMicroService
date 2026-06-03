using CatalogService.Api.Features.Banka.Dtos;
using CatalogService.Api.Features.Banka.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Banka.Controllers
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class BankaTakipController : ControllerBase
    {
        private readonly IBankaTakipService _service;

        public BankaTakipController(IBankaTakipService service)
        {
            _service = service;
        }

        // Aylık takip görünümü: aktif hesaplar + ay içinde işlenen günleri.
        [HttpGet]
        [ProducesResponseType(typeof(List<HesapTakipDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<HesapTakipDto>>> GetAy(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] int? firmaId = null,
            CancellationToken ct = default)
        {
            var list = await _service.GetAyAsync(year, month, firmaId);
            return Ok(list);
        }

        // Bir hesabın bir gününü işlendi/işlenmedi olarak işaretler (upsert).
        [HttpPost("isaretle")]
        [ProducesResponseType(typeof(IslemKaydiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IslemKaydiDto>> Isaretle([FromBody] IsaretleRequestDto dto, CancellationToken ct)
        {
            var result = await _service.IsaretleAsync(dto);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
