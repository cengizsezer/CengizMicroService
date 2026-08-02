using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Features.FirmaKontrol.Services;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.FirmaKontrol.Controllers
{
    /// <summary>
    /// Kurumlar vergisi beyannamesi: kalem katalogu ve firma bazlı beyanname girdileri.
    /// İş kuralları servistedir; buradaki kontroller yalnızca yönlendirme içindir.
    /// </summary>
    [ApiController]
    [Route("api/catalog/firma-kontrol/vergi")]
    public class VergiBeyannameController : ControllerBase
    {
        private readonly IVergiBeyannameService _service;

        public VergiBeyannameController(IVergiBeyannameService service) => _service = service;

        // ── Kalem katalogu ──

        /// <summary>Beyanname kalemleri; varsayılan olarak yalnızca aktif olanlar.</summary>
        [HttpGet("kalemler")]
        [ProducesResponseType(typeof(List<VergiKalemiDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<VergiKalemiDto>>> GetKalemler([FromQuery] bool pasifDahil, CancellationToken ct)
            => Ok(await _service.GetKalemlerAsync(pasifDahil, ct));

        [HttpGet("kalemler/{id:int}")]
        [ProducesResponseType(typeof(VergiKalemiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VergiKalemiDto>> GetKalem(int id, CancellationToken ct)
        {
            var dto = await _service.GetKalemAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPost("kalemler")]
        [ProducesResponseType(typeof(VergiKalemiDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<VergiKalemiDto>> KalemEkle([FromBody] VergiKalemiYazDto dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.KalemEkleAsync(dto, ct);
                return CreatedAtAction(nameof(GetKalem), new { id = created.Id }, created);
            }
            catch (VergiKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("kalemler/{id:int}")]
        [ProducesResponseType(typeof(VergiKalemiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VergiKalemiDto>> KalemGuncelle(int id, [FromBody] VergiKalemiYazDto dto, CancellationToken ct)
        {
            try
            {
                var updated = await _service.KalemGuncelleAsync(id, dto, ct);
                return updated is null ? NotFound() : Ok(updated);
            }
            catch (VergiKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Kalemi pasife çeker; geçmiş beyannamelerde görünmeye devam eder.</summary>
        [HttpPatch("kalemler/{id:int}/pasif")]
        [ProducesResponseType(typeof(VergiKalemiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VergiKalemiDto>> KalemPasifeAl(int id, CancellationToken ct)
        {
            var dto = await _service.KalemPasifeAlAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpDelete("kalemler/{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> KalemSil(int id, CancellationToken ct)
        {
            var sonuc = await _service.KalemSilAsync(id, ct);
            return sonuc switch
            {
                KalemSilmeSonuc.Silindi => NoContent(),
                KalemSilmeSonuc.SistemKalemi => Conflict(new { message = "Sistem kalemleri silinemez. Kullanmayacaksanız kalemi pasife alın." }),
                KalemSilmeSonuc.Kullanilmis => Conflict(new { message = "Bu kalem bir beyannamede kullanılmış; silinemez. Kalemi pasife alın." }),
                _ => NotFound()
            };
        }

        [HttpPost("kalemler/sirala")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Sirala([FromBody] List<VergiKalemSiraDto> sira, CancellationToken ct)
        {
            await _service.SiralamayiKaydetAsync(sira ?? new(), ct);
            return NoContent();
        }

        // ── Beyanname ──

        /// <summary>Firmanın dönemine ait kayıtlı beyanname; yoksa 204.</summary>
        [HttpGet("{firmaId:int}/{donemYil:int}")]
        [ProducesResponseType(typeof(VergiBeyannameDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult<VergiBeyannameDto>> GetBeyanname(int firmaId, short donemYil, CancellationToken ct)
        {
            var dto = await _service.GetBeyannameAsync(firmaId, donemYil, ct);
            return dto is null ? NoContent() : Ok(dto);
        }

        /// <summary>Kaydetmeden hesaplar; ekranın canlı önizlemesi bunu kullanır.</summary>
        [HttpPost("onizle")]
        [ProducesResponseType(typeof(VergiSonucDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<VergiSonucDto>> Onizle([FromBody] VergiBeyannameYazDto dto, CancellationToken ct)
            => Ok(await _service.OnizleAsync(dto, ct));

        /// <summary>Beyanname formatına yakın .xlsx; kayıt yoksa 404.</summary>
        [HttpGet("{firmaId:int}/{donemYil:int}/excel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Excel(int firmaId, short donemYil, CancellationToken ct)
        {
            var sonuc = await _service.ExcelAsync(firmaId, donemYil, ct);
            if (sonuc is null)
                return NotFound(new { message = "Bu dönem için kayıtlı vergi hesaplaması yok. Önce kaydedin." });

            return File(sonuc.Value.Icerik,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                sonuc.Value.DosyaAdi);
        }

        [HttpPost("{firmaId:int}")]
        [ProducesResponseType(typeof(VergiBeyannameDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VergiBeyannameDto>> Kaydet(int firmaId, [FromBody] VergiBeyannameYazDto dto, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.KaydetAsync(firmaId, dto, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (VergiKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }
    }
}
