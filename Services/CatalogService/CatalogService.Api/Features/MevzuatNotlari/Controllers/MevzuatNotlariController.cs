using CatalogService.Api.Features.MevzuatNotlari.Dtos;
using CatalogService.Api.Features.MevzuatNotlari.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.MevzuatNotlari.Controllers
{
    [ApiController]
    [Route("api/catalog/mevzuat-notlari")]
    [Authorize]
    public class MevzuatNotlariController : ControllerBase
    {
        private readonly IMevzuatNotuService _service;

        public MevzuatNotlariController(IMevzuatNotuService service) => _service = service;

        [HttpGet]
        [ProducesResponseType(typeof(List<MevzuatNotuDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<MevzuatNotuDto>>> GetAll(
            [FromQuery] string? kategori,
            [FromQuery] string? arama,
            CancellationToken ct)
            => Ok(await _service.GetAllAsync(kategori, arama, ct));

        [HttpGet("kategori-sayilari")]
        [ProducesResponseType(typeof(Dictionary<string, int>), StatusCodes.Status200OK)]
        public async Task<ActionResult<Dictionary<string, int>>> GetKategoriSayilari(CancellationToken ct)
            => Ok(await _service.GetKategoriSayilariAsync(ct));

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(MevzuatNotuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MevzuatNotuDto>> GetById(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(MevzuatNotuDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MevzuatNotuDto>> Create([FromBody] MevzuatNotuDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Kategori)) return BadRequest(new { message = "Kategori zorunlu." });
            if (string.IsNullOrWhiteSpace(dto.Baslik)) return BadRequest(new { message = "Başlık zorunlu." });

            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(MevzuatNotuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MevzuatNotuDto>> Update(int id, [FromBody] MevzuatNotuDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Kategori)) return BadRequest(new { message = "Kategori zorunlu." });
            if (string.IsNullOrWhiteSpace(dto.Baslik)) return BadRequest(new { message = "Başlık zorunlu." });

            var updated = await _service.UpdateAsync(id, dto, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var ok = await _service.DeleteAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
    }
}
