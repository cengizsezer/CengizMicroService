using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Yapilacaklar.Controllers
{
    /// <summary>
    /// Takip panosunun "Kişiler ve mailler" sheet'i (Prompt 14). Firma üstü okuma; yazma firmayı formdan
    /// (ekleme) ya da kişi kaydından (düzenleme) alır. Rota <c>api/catalog/*</c>, gateway değişmedi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/kisiler")]
    [Authorize]
    public class KisilerController : ControllerBase
    {
        private readonly IKisiService _service;

        public KisilerController(IKisiService service) => _service = service;

        /// <summary>Tablo + ayın mail takvimi (ay verilmezse içinde bulunulan ay) + şüpheli eşleşmeler.</summary>
        [HttpGet("pano")]
        public Task<ActionResult<KisilerPanosuDto>> Pano([FromQuery] int? yil, [FromQuery] int? ay, [FromQuery] int? firmaId,
                                                         CancellationToken ct = default)
            => Calistir(() => _service.PanoAsync(yil, ay, ct, firmaId));

        /// <summary>İş dialogunun alıcı önizlemesi — kaydetmez; e-posta/ad kişi kaydından gelecekse söyler.</summary>
        [HttpPost("onizle")]
        public Task<ActionResult<List<AliciOnizlemeDto>>> Onizle([FromBody] AliciOnizlemeIstegiDto istek, CancellationToken ct = default)
            => Calistir(() => _service.OnizleAsync(istek, ct));

        [HttpPost]
        public Task<ActionResult<KisiOzetDto>> Ekle([FromBody] KisiKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkleAsync(dto, ct));

        [HttpPut("{id:int}")]
        public Task<ActionResult<KisiOzetDto>> Guncelle(int id, [FromBody] KisiKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.GuncelleAsync(id, dto, ct));

        /// <summary>Kullanıcının kararıyla: kaynağın alıcıları hedefe geçer, kaynak silinir.</summary>
        [HttpPost("{hedefId:int}/birlestir/{kaynakId:int}")]
        public Task<ActionResult<bool>> Birlestir(int hedefId, int kaynakId, CancellationToken ct = default)
            => Calistir(async () => { await _service.BirlestirAsync(hedefId, kaynakId, ct); return true; });

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
