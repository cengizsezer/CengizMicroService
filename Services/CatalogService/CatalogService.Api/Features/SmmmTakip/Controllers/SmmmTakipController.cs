using CatalogService.Api.Features.SmmmTakip.Dtos;
using CatalogService.Api.Features.SmmmTakip.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.SmmmTakip.Controllers
{
    [ApiController]
    [Route("api/catalog/smmm-takip")]
    [Authorize]
    public class SmmmTakipController : ControllerBase
    {
        private readonly ISmmmTakipService _service;

        public SmmmTakipController(ISmmmTakipService service) => _service = service;

        // ---- Okuma (giriş yapmış herkes) ----

        [HttpGet("agac")]
        public async Task<ActionResult<List<SmmmKonuAgacDto>>> GetAgac(CancellationToken ct)
            => Ok(await _service.GetAgacAsync(ct));

        [HttpGet("konu/{slug}")]
        public async Task<ActionResult<SmmmKonuDetayDto>> GetKonuBySlug(string slug, [FromQuery] int? yil, CancellationToken ct)
        {
            var dto = await _service.GetKonuBySlugAsync(slug, yil, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpGet("admin/konu/{id:int}")]
        public async Task<ActionResult<SmmmKonuDetayDto>> GetKonuByIdForEdit(int id, [FromQuery] int? yil, CancellationToken ct)
        {
            var dto = await _service.GetKonuByIdAsync(id, yil, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpGet("konu/{konuId:int}/hadler")]
        public async Task<ActionResult<List<SmmmHadDto>>> GetHadler(int konuId, [FromQuery] int? yil, CancellationToken ct)
        {
            var hadler = await _service.GetHadlerAsync(konuId, yil, ct);
            return hadler is null ? NotFound() : Ok(hadler);
        }

        // ---- Konu (admin) ----

        [HttpPost("admin/konu")]
        public async Task<ActionResult<SmmmKonuDetayDto>> CreateKonu([FromBody] SmmmKonuSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Baslik)) return BadRequest(new { message = "Başlık zorunlu." });
            var created = await _service.CreateKonuAsync(dto, ct);
            return CreatedAtAction(nameof(GetKonuByIdForEdit), new { id = created.Id }, created);
        }

        [HttpPut("admin/konu/{id:int}")]
        public async Task<ActionResult<SmmmKonuDetayDto>> UpdateKonu(int id, [FromBody] SmmmKonuSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Baslik)) return BadRequest(new { message = "Başlık zorunlu." });
            var updated = await _service.UpdateKonuAsync(id, dto, ct);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpDelete("admin/konu/{id:int}")]
        public async Task<IActionResult> DeleteKonu(int id, CancellationToken ct)
        {
            var sonuc = await _service.DeleteKonuAsync(id, ct);
            return sonuc switch
            {
                KonuSilmeSonuc.Silindi => NoContent(),
                KonuSilmeSonuc.AltKonuVar => Conflict(new { message = "Alt konusu olan bir konu silinemez. Önce alt konuları silin veya taşıyın." }),
                _ => NotFound()
            };
        }

        // ---- Had (admin) ----

        [HttpPost("admin/hadler")]
        public async Task<ActionResult<SmmmHadDto>> CreateHad([FromBody] SmmmHadSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Kod)) return BadRequest(new { message = "Kod zorunlu." });
            if (string.IsNullOrWhiteSpace(dto.Ad)) return BadRequest(new { message = "Ad zorunlu." });
            try
            {
                var had = await _service.CreateHadAsync(dto, ct);
                return had is null ? NotFound(new { message = "Konu bulunamadı." }) : Ok(had);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("admin/hadler/{id:int}")]
        public async Task<ActionResult<SmmmHadDto>> UpdateHad(int id, [FromBody] SmmmHadSaveDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Kod)) return BadRequest(new { message = "Kod zorunlu." });
            if (string.IsNullOrWhiteSpace(dto.Ad)) return BadRequest(new { message = "Ad zorunlu." });
            try
            {
                var had = await _service.UpdateHadAsync(id, dto, ct);
                return had is null ? NotFound() : Ok(had);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpDelete("admin/hadler/{id:int}")]
        public async Task<IActionResult> DeleteHad(int id, CancellationToken ct)
            => await _service.DeleteHadAsync(id, ct) ? NoContent() : NotFound();

        // ---- Had değeri (admin) — yıl bazlı upsert ----

        [HttpPut("admin/had-degeri")]
        public async Task<ActionResult<SmmmHadDegeriDto>> UpsertHadDegeri([FromBody] SmmmHadDegeriSaveDto dto, CancellationToken ct)
        {
            if (dto.Yil < 2000 || dto.Yil > 2100) return BadRequest(new { message = "Geçerli bir yıl girin." });
            var deger = await _service.UpsertHadDegeriAsync(dto, ct);
            return deger is null ? NotFound(new { message = "Had bulunamadı." }) : Ok(deger);
        }

        [HttpDelete("admin/had-degeri/{id:int}")]
        public async Task<IActionResult> DeleteHadDegeri(int id, CancellationToken ct)
            => await _service.DeleteHadDegeriAsync(id, ct) ? NoContent() : NotFound();
    }
}
