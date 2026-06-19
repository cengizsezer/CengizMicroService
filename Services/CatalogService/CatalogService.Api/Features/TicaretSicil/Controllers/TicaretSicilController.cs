using CatalogService.Api.Features.TicaretSicil.Dtos;
using CatalogService.Api.Features.TicaretSicil.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.TicaretSicil.Controllers
{
    [ApiController]
    [Route("api/catalog/ticaret-sicil")]
    [Authorize]
    public class TicaretSicilController : ControllerBase
    {
        private readonly ITicaretSicilService _service;

        public TicaretSicilController(ITicaretSicilService service) => _service = service;

        // ---- Okuma (giriş yapmış herkes) ----

        [HttpGet]
        public async Task<ActionResult<List<TicaretSicilIslemListDto>>> GetAll(CancellationToken ct)
            => Ok(await _service.GetAllAsync(ct));

        [HttpGet("{slug}")]
        public async Task<ActionResult<TicaretSicilIslemDetayDto>> GetBySlug(string slug, CancellationToken ct)
        {
            var dto = await _service.GetBySlugAsync(slug, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpGet("admin/{id:int}")]
        public async Task<ActionResult<TicaretSicilIslemDetayDto>> GetByIdForEdit(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        // ---- İşlem (admin) ----

        [HttpPost("admin")]
        public async Task<ActionResult<TicaretSicilIslemDetayDto>> CreateIslem([FromBody] TicaretSicilIslemSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Ad)) return BadRequest(new { message = "Ad zorunlu." });
            var created = await _service.CreateIslemAsync(dto, ct);
            return CreatedAtAction(nameof(GetByIdForEdit), new { id = created.Id }, created);
        }

        [HttpPut("admin/{id:int}")]
        public async Task<ActionResult<TicaretSicilIslemDetayDto>> UpdateIslem(int id, [FromBody] TicaretSicilIslemSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Ad)) return BadRequest(new { message = "Ad zorunlu." });
            var updated = await _service.UpdateIslemAsync(id, dto, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpDelete("admin/{id:int}")]
        public async Task<ActionResult<List<int>>> DeleteIslem(int id, CancellationToken ct)
        {
            var fileIds = await _service.DeleteIslemAsync(id, ct);
            return fileIds is null ? NotFound() : Ok(fileIds);
        }

        // ---- Adım (admin) ----

        [HttpPost("admin/{islemId:int}/adimlar")]
        public async Task<ActionResult<TicaretSicilAdimDto>> AddAdim(int islemId, [FromBody] TicaretSicilAdimSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Baslik)) return BadRequest(new { message = "Başlık zorunlu." });
            var adim = await _service.AddAdimAsync(islemId, dto, ct);
            return adim is null ? NotFound() : Ok(adim);
        }

        [HttpPut("admin/adimlar/{adimId:int}")]
        public async Task<ActionResult<TicaretSicilAdimDto>> UpdateAdim(int adimId, [FromBody] TicaretSicilAdimSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Baslik)) return BadRequest(new { message = "Başlık zorunlu." });
            var adim = await _service.UpdateAdimAsync(adimId, dto, ct);
            return adim is null ? NotFound() : Ok(adim);
        }

        [HttpDelete("admin/adimlar/{adimId:int}")]
        public async Task<ActionResult<List<int>>> DeleteAdim(int adimId, CancellationToken ct)
        {
            var fileIds = await _service.DeleteAdimAsync(adimId, ct);
            return fileIds is null ? NotFound() : Ok(fileIds);
        }

        [HttpPost("admin/adimlar/{adimId:int}/move")]
        public async Task<IActionResult> MoveAdim(int adimId, [FromQuery] string direction, CancellationToken ct)
        {
            var ok = await _service.MoveAdimAsync(adimId, direction, ct);
            return ok ? NoContent() : BadRequest(new { message = "Taşınamadı." });
        }

        // ---- Ek / belge (admin) ----

        [HttpPost("admin/adimlar/{adimId:int}/ekler")]
        public async Task<ActionResult<TicaretSicilEkDto>> AddEk(int adimId, [FromBody] TicaretSicilEkSaveDto dto, CancellationToken ct)
        {
            if (dto.FileId <= 0) return BadRequest(new { message = "Geçerli bir dosya referansı (FileId) gerekli." });
            var ek = await _service.AddEkAsync(adimId, dto, ct);
            return ek is null ? NotFound() : Ok(ek);
        }

        [HttpDelete("admin/ekler/{ekId:int}")]
        public async Task<ActionResult<int>> DeleteEk(int ekId, CancellationToken ct)
        {
            var fileId = await _service.DeleteEkAsync(ekId, ct);
            return fileId is null ? NotFound() : Ok(fileId.Value);
        }
    }
}
