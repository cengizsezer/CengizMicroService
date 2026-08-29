using System.Security.Claims;
using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Declarations.Controllers
{
    /// <summary>
    /// Beyanname özeti (firma × tür matrisi) ve beyanname belgeleri. Tür tanımlarının
    /// uçları <see cref="BeyannameTurleriController"/>'da.
    /// Rota <c>api/catalog/*</c> altında olduğu için gateway'in mevcut
    /// <c>/catalog/{everything}</c> route'undan değişiklik olmadan geçer.
    /// </summary>
    [ApiController]
    [Route("api/catalog/beyanname")]
    [Authorize]
    public class BeyannameOzetController : ControllerBase
    {
        private readonly IBeyannameOzetService _ozet;
        private readonly IBeyannameEkService _ekler;

        public BeyannameOzetController(IBeyannameOzetService ozet, IBeyannameEkService ekler)
        {
            _ozet = ozet;
            _ekler = ekler;
        }

        /// <summary>Bir dönemin firma × tür matrisi.</summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<BeyannameOzetDto>> Ozet([FromQuery] int yil, [FromQuery] int ay,
                                                               CancellationToken ct = default)
        {
            try
            {
                return Ok(await _ozet.GetAsync(yil, ay, ct));
            }
            catch (BeyannameKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        // ---- Belgeler ----

        [HttpGet("{declarationId:int}/ekler")]
        public async Task<ActionResult<List<BeyannameEkDto>>> Ekler(int declarationId, CancellationToken ct = default)
            => Ok(await _ekler.GetAsync(declarationId, ct));

        /// <summary>
        /// Belge kaydı. Dosyanın kendisi <b>önce</b> FileApiService'e yüklenir; buraya
        /// yalnız dönen <c>FileId</c> ve metadata gelir.
        ///
        /// Yanıttaki <c>artikFileId</c> dolu ise aynı türden eski bir belge değiştirilmiştir
        /// ve o dosya artık sahipsizdir; istemci onu FileApiService'ten silmelidir.
        /// </summary>
        [HttpPost("{declarationId:int}/ekler")]
        public async Task<ActionResult<object>> EkEkle(int declarationId, [FromBody] BeyannameEkOlusturDto istek,
                                                       CancellationToken ct = default)
        {
            try
            {
                var sonuc = await _ekler.EkleAsync(declarationId, istek, KullaniciAdi(), ct);
                return Ok(new { ek = sonuc.Ek, artikFileId = sonuc.ArtikFileId });
            }
            catch (BeyannameKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Belgeyi siler; yanıttaki <c>fileId</c> FileApiService'ten de silinmelidir.</summary>
        [HttpDelete("{declarationId:int}/ekler/{ekId:int}")]
        public async Task<ActionResult<object>> EkSil(int declarationId, int ekId, CancellationToken ct = default)
        {
            try
            {
                var fileId = await _ekler.SilAsync(declarationId, ekId, ct);
                return Ok(new { fileId });
            }
            catch (BeyannameKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        private string? KullaniciAdi()
            => User?.FindFirstValue(ClaimTypes.Name)
               ?? User?.FindFirstValue("preferred_username")
               ?? User?.Identity?.Name;
    }
}
