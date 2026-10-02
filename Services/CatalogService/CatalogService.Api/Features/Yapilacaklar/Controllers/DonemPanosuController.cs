using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Yapilacaklar.Controllers
{
    /// <summary>
    /// Dönem panosu: firma × iş matrisi ve hücre paneli. Firma üstü (Yapılacaklar gibi); her
    /// hücre servis tarafında tek tek doğrulanır. İşaretleme <c>PUT yapilacaklar/isaretle</c>
    /// ile yapılır — aynı tamamlama kaydı. Rota <c>api/catalog/*</c>, gateway değişmedi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/donem-panosu")]
    [Authorize]
    public class DonemPanosuController : ControllerBase
    {
        private readonly IDonemPanosuService _service;

        public DonemPanosuController(IDonemPanosuService service) => _service = service;

        /// <summary>Ay verilmezse bir önceki ay. <c>firmaId</c> doluysa yalnız o firma (sayılar dahil).</summary>
        [HttpGet]
        public Task<ActionResult<DonemPanosuDto>> Pano([FromQuery] int? yil, [FromQuery] int? ay, [FromQuery] int? firmaId,
                                                       CancellationToken ct = default)
            => Calistir(() => _service.PanoAsync(yil, ay, ct, firmaId));

        /// <summary>Sekme şeridi + Özet sheet: içinde bulunulan ay ve önceki aylar, her biri kendi matrisinden.</summary>
        [HttpGet("ozet")]
        public Task<ActionResult<DonemOzetiDto>> Ozet(CancellationToken ct = default)
            => Calistir(() => _service.OzetAsync(ct));

        [HttpGet("hucre")]
        public Task<ActionResult<DonemHucresiDto>> Hucre([FromQuery] IsKaynagi kaynakTip, [FromQuery] int kaynakId,
                                                         [FromQuery] int firmaId, [FromQuery] string donemAnahtari,
                                                         CancellationToken ct = default)
            => Calistir(() => _service.HucreAsync(new IsIsaretDto
            {
                KaynakTip = kaynakTip, KaynakId = kaynakId, FirmaId = firmaId, DonemAnahtari = donemAnahtari
            }, ct));

        [HttpPut("hucre/not")]
        public async Task<IActionResult> Not([FromBody] DonemNotuDto dto, CancellationToken ct = default)
        {
            try
            {
                await _service.NotKaydetAsync(dto, ct);
                return NoContent();
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>
        /// "Bu dönem kaydını sil": işaret, not ve kanıtların tamamı. Yanıttaki <c>fileIds</c>
        /// FileApiService'ten de silinmelidir. (İşaret kaldırmak not/kanıtı silmez.)
        /// </summary>
        [HttpDelete("hucre")]
        public Task<ActionResult<object>> KayitSil([FromQuery] IsKaynagi kaynakTip, [FromQuery] int kaynakId,
                                                   [FromQuery] int firmaId, [FromQuery] string donemAnahtari,
                                                   CancellationToken ct = default)
            => Calistir<object>(async () => new
            {
                fileIds = await _service.KayitSilAsync(new IsIsaretDto
                {
                    KaynakTip = kaynakTip, KaynakId = kaynakId, FirmaId = firmaId, DonemAnahtari = donemAnahtari
                }, ct)
            });

        /// <summary>Dönem eki. Dosya önce FileApiService'e yüklenir (prosedür ekiyle aynı akış ve sınır).</summary>
        [HttpPost("hucre/ekler")]
        public Task<ActionResult<FirmaIsiEkiDto>> EkEkle([FromBody] DonemEkiOlusturDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkEkleAsync(dto, ct));

        /// <summary>Yanıttaki <c>fileId</c> FileApiService'ten de silinmelidir.</summary>
        [HttpDelete("hucre/ekler/{ekId:int}")]
        public async Task<ActionResult<object>> EkSil(int ekId, CancellationToken ct = default)
        {
            try
            {
                return Ok(new { fileId = await _service.EkSilAsync(ekId, ct) });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "ekId", message = ex.Message });
            }
        }

        private async Task<ActionResult<T>> Calistir<T>(Func<Task<T>> is_)
        {
            try
            {
                return Ok(await is_());
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "id", message = ex.Message });
            }
        }
    }
}
