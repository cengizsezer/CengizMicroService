using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Features.FirmaKontrol.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.FirmaKontrol.Controllers
{
    [ApiController]
    [Route("api/catalog/firma-kontrol")]
    public class FirmaKontrolController : ControllerBase
    {
        private readonly IFirmaKontrolMaddeService _service;
        private readonly IFirmaKontrolMizanService _mizan;
        private readonly IFirmaKontrolVergiService _vergi;

        public FirmaKontrolController(
            IFirmaKontrolMaddeService service,
            IFirmaKontrolMizanService mizan,
            IFirmaKontrolVergiService vergi)
        {
            _service = service;
            _mizan = mizan;
            _vergi = vergi;
        }

        // ── Durum satırları (şablon durumları + özel maddeler) ──────────────

        [HttpGet("{firmaId:int}/maddeler")]
        [ProducesResponseType(typeof(List<FirmaKontrolMaddeDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<FirmaKontrolMaddeDto>>> GetMaddeler(int firmaId, CancellationToken ct)
        {
            var list = await _service.GetDurumlarAsync(firmaId, ct);
            return Ok(list);
        }

        // ── Tek madde durum/not upsert (şablon: MaddeKey, özel: Id) ─────────

        [HttpPut("{firmaId:int}/maddeler")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpsertMadde(
            int firmaId,
            [FromBody] FirmaKontrolMaddeUpsertDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                await _service.UpsertDurumAsync(firmaId, dto, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Özel madde ekle ─────────────────────────────────────────────────

        [HttpPost("{firmaId:int}/maddeler/ozel")]
        [ProducesResponseType(typeof(FirmaKontrolMaddeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<FirmaKontrolMaddeDto>> AddOzel(
            int firmaId,
            [FromBody] OzelMaddeCreateDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                var created = await _service.AddOzelAsync(firmaId, dto, ct);
                return CreatedAtAction(nameof(GetMaddeler), new { firmaId }, created);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Özel madde metnini düzenle ──────────────────────────────────────

        [HttpPut("{firmaId:int}/maddeler/ozel/{id:long}")]
        [ProducesResponseType(typeof(FirmaKontrolMaddeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<FirmaKontrolMaddeDto>> UpdateOzel(
            int firmaId,
            long id,
            [FromBody] OzelMaddeUpdateDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                var updated = await _service.UpdateOzelAsync(firmaId, id, dto, ct);
                return updated is null
                    ? NotFound(new { mesaj = $"Özel madde bulunamadı: Id={id}" })
                    : Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Özel madde sil ──────────────────────────────────────────────────

        [HttpDelete("{firmaId:int}/maddeler/ozel/{id:long}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteOzel(int firmaId, long id, CancellationToken ct)
        {
            var ok = await _service.DeleteOzelAsync(firmaId, id, ct);
            return ok ? NoContent() : NotFound(new { mesaj = $"Özel madde bulunamadı: Id={id}" });
        }

        // ── Ham mizan: kaydet / getir / sıfırla ─────────────────────────────

        [HttpGet("{firmaId:int}/mizan")]
        [ProducesResponseType(typeof(List<FirmaKontrolMizanSatirDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<FirmaKontrolMizanSatirDto>>> GetMizan(
            int firmaId,
            [FromQuery] int yil,
            CancellationToken ct)
        {
            var list = await _mizan.GetSatirlarAsync(firmaId, yil, ct);
            return Ok(list);
        }

        [HttpPost("{firmaId:int}/mizan")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> KaydetMizan(
            int firmaId,
            [FromBody] MizanKaydetRequest req,
            CancellationToken ct)
        {
            if (req is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                await _mizan.KaydetAsync(firmaId, req, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
        }

        [HttpDelete("{firmaId:int}/mizan")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> SifirlaMizan(
            int firmaId,
            [FromQuery] int yil,
            CancellationToken ct)
        {
            await _mizan.SifirlaAsync(firmaId, yil, ct);
            return NoContent();
        }

        // ── Vergi paneli girdileri: getir / kaydet ──────────────────────────

        [HttpGet("{firmaId:int}/vergi")]
        [ProducesResponseType(typeof(FirmaKontrolVergiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult<FirmaKontrolVergiDto>> GetVergi(
            int firmaId,
            [FromQuery] int donem,
            [FromQuery] int yil,
            CancellationToken ct)
        {
            var dto = await _vergi.GetAsync(firmaId, donem, yil, ct);
            return dto is null ? NoContent() : Ok(dto);
        }

        [HttpPost("{firmaId:int}/vergi")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> KaydetVergi(
            int firmaId,
            [FromBody] FirmaKontrolVergiDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                await _vergi.UpsertAsync(firmaId, dto, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
        }
    }
}
