using System.Security.Claims;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;
using CatalogService.Api.Features.FirmaBilgileri.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.FirmaBilgileri.Controllers
{
    /// <summary>
    /// Firma Bilgileri: sicil, ortaklık, imza yetkilileri, belgeler.
    ///
    /// Kapsam Banka Otomasyon'daki mekanizmanın aynısı: <c>?firmaId=</c> zorunlu,
    /// <see cref="BankaFirmaFiltresi"/> parametreyi doğrulayıp kapsamı kuruyor. Eksik ya
    /// da tanınmayan firma 400 döner — sessiz varsayılan yok.
    ///
    /// Rota <c>api/catalog/*</c> altında olduğu için gateway değişmedi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/firma-bilgileri")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class FirmaBilgileriController : ControllerBase
    {
        private readonly IFirmaBilgiService _service;

        public FirmaBilgileriController(IFirmaBilgiService service) => _service = service;

        // ---- Sicil ----

        [HttpGet("sicil")]
        public async Task<ActionResult<FirmaSicilDto>> Sicil(CancellationToken ct = default)
            => await Calistir(() => _service.SicilGetAsync(ct));

        [HttpPut("sicil")]
        public async Task<ActionResult<FirmaSicilDto>> SicilKaydet([FromBody] FirmaSicilDto dto,
                                                                   CancellationToken ct = default)
            => await Calistir(() => _service.SicilKaydetAsync(dto, ct));

        // ---- Ortaklık ----

        [HttpGet("ortaklar")]
        public async Task<ActionResult<FirmaOrtaklikDto>> Ortaklar(CancellationToken ct = default)
            => await Calistir(() => _service.OrtaklarGetAsync(ct));

        /// <summary>Ortaklık tablosunu bütün olarak kaydeder; gönderilmeyen satır silinir.</summary>
        [HttpPut("ortaklar")]
        public async Task<ActionResult<FirmaOrtaklikDto>> OrtaklarKaydet([FromBody] List<FirmaOrtakDto> ortaklar,
                                                                         CancellationToken ct = default)
            => await Calistir(() => _service.OrtaklarKaydetAsync(ortaklar ?? new(), ct));

        // ---- İmza yetkilileri ----

        [HttpGet("imza-yetkilileri")]
        public async Task<ActionResult<List<FirmaImzaYetkilisiDto>>> Yetkililer(CancellationToken ct = default)
            => await Calistir(() => _service.YetkililerGetAsync(ct));

        [HttpPut("imza-yetkilileri")]
        public async Task<ActionResult<List<FirmaImzaYetkilisiDto>>> YetkililerKaydet(
            [FromBody] List<FirmaImzaYetkilisiDto> yetkililer, CancellationToken ct = default)
            => await Calistir(() => _service.YetkililerKaydetAsync(yetkililer ?? new(), ct));

        // ---- Belgeler ----

        [HttpGet("belgeler")]
        public async Task<ActionResult<List<FirmaBelgesiDto>>> Belgeler(CancellationToken ct = default)
            => await Calistir(() => _service.BelgelerGetAsync(ct));

        /// <summary>
        /// Belge kaydı. Dosya <b>önce</b> FileApiService'e yüklenir; buraya dönen
        /// <c>FileId</c> ve metadata gelir (beyanname ekleriyle aynı akış).
        /// </summary>
        [HttpPost("belgeler")]
        public async Task<ActionResult<FirmaBelgesiDto>> BelgeEkle([FromBody] FirmaBelgesiOlusturDto istek,
                                                                   CancellationToken ct = default)
            => await Calistir(() => _service.BelgeEkleAsync(istek, KullaniciAdi(), ct));

        /// <summary>Belgeyi siler; yanıttaki <c>fileId</c> FileApiService'ten de silinmelidir.</summary>
        [HttpDelete("belgeler/{belgeId:int}")]
        public async Task<ActionResult<object>> BelgeSil(int belgeId, CancellationToken ct = default)
        {
            try
            {
                var fileId = await _service.BelgeSilAsync(belgeId, ct);
                return Ok(new { fileId });
            }
            catch (FirmaBilgiKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        private async Task<ActionResult<T>> Calistir<T>(Func<Task<T>> is_)
        {
            try
            {
                return Ok(await is_());
            }
            catch (FirmaBilgiKuralException ex)
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
