using CatalogService.Api.Features.FirmaPortfoyu.Dtos;
using CatalogService.Api.Features.FirmaPortfoyu.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.FirmaPortfoyu.Controllers
{
    /// <summary>
    /// Firma Portföyü (Aşama 1): aylık mizan yükleme ve firma × ay durum matrisi. Rota <c>api/catalog/*</c>;
    /// gateway'in genel <c>/catalog/{everything}</c> rotası yeterli, Ocelot değişmedi.
    ///
    /// Dosya gelmez: istemci okur, ayrıştırılmış ana hesap satırlarını JSON gönderir. Aynı firma + ay varsa
    /// POST mizan 409 döner (gövde: önizleme + mevcut); onaylanınca <c>uzerineYaz=true</c> ile yeniden gönderilir.
    /// </summary>
    [ApiController]
    [Route("api/catalog/firma-portfoyu")]
    [Authorize]
    public class FirmaPortfoyuController : ControllerBase
    {
        private readonly IFirmaPortfoyuService _service;

        public FirmaPortfoyuController(IFirmaPortfoyuService service) => _service = service;

        /// <summary>
        /// Aşama 2: seçili dönemde her aktif firmanın göstergeleri ve 12 aylık serisi. <c>donem</c> boşsa mizanı olan
        /// en son ay. Firmalar arası toplam yok.
        /// </summary>
        [HttpGet]
        public Task<ActionResult<PortfoyDto>> Portfoy([FromQuery] string? donem, [FromQuery] string? karsilastir,
                                                      CancellationToken ct = default)
            => Calistir(() => _service.PortfoyAsync(donem, karsilastir, ct));

        /// <summary>Kaydetmeden değerlendirir: toplamlar, denge, kümülatif net satış/SMM, uyarılar, aynı ay var mı.</summary>
        [HttpPost("mizan/onizle")]
        public Task<ActionResult<AylikMizanOnizlemeDto>> Onizle([FromBody] AylikMizanKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.OnizleAsync(dto, ct));

        [HttpPost("mizan")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public Task<ActionResult<AylikMizanOnizlemeDto>> Kaydet([FromBody] AylikMizanKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.KaydetAsync(dto, ct));

        [HttpDelete("mizan/{firmaId:int}/{yil:int}/{ay:int}")]
        public async Task<IActionResult> Sil(int firmaId, int yil, int ay, CancellationToken ct = default)
        {
            try
            {
                await _service.SilAsync(firmaId, yil, ay, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "ay", message = ex.Message });
            }
        }

        /// <summary>Aktif firmalar × 12 ay: yüklendi mi, dengeli mi, uyarılar.</summary>
        [HttpGet("mizan-durumu")]
        public Task<ActionResult<MizanDurumuDto>> Durum([FromQuery] int yil, CancellationToken ct = default)
            => Calistir(() => _service.DurumAsync(yil, ct));

        private async Task<ActionResult<T>> Calistir<T>(Func<Task<T>> is_)
        {
            try
            {
                return Ok(await is_());
            }
            catch (FirmaPortfoyuKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (AyniAyVarException ex)
            {
                return Conflict(ex.Onizleme);
            }
        }
    }
}
