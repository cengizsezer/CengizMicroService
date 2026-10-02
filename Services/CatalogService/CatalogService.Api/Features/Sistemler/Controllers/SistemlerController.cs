using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.Sistemler.Domain;
using CatalogService.Api.Features.Sistemler.Dtos;
using CatalogService.Api.Features.Sistemler.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Sistemler.Controllers
{
    /// <summary>
    /// Ortak sistem listesi. Global tablo, firma kapsamı yok. Ekleme hem Yönetim →
    /// Sistemler'den hem kartın "+ Yeni ekle"sinden aynı uca gelir — kullanıcı firma girerken
    /// Yönetim ekranına gitmek zorunda kalmaz. Rota <c>api/catalog/*</c>, gateway değişmedi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/sistemler")]
    [Authorize]
    public class SistemlerController : ControllerBase
    {
        private readonly ISistemService _service;

        public SistemlerController(ISistemService service) => _service = service;

        /// <summary>Açılır liste <c>pasiflerDahil=false</c> ister; Yönetim ekranı hepsini.</summary>
        [HttpGet]
        public async Task<ActionResult<List<SistemDto>>> Liste([FromQuery] SistemTuru? tur, [FromQuery] bool pasiflerDahil = false,
                                                               CancellationToken ct = default)
            => Ok(await _service.ListeAsync(tur, pasiflerDahil, ct));

        /// <summary>Benzer ad varsa ve <c>Israr</c> yoksa 409 + benzer kayıt.</summary>
        [HttpPost]
        public Task<ActionResult<SistemDto>> Ekle([FromBody] SistemEkleDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkleAsync(dto, ct));

        /// <summary>Yeniden adlandırma ve pasife alma. Yeni ad için de benzer ad uyarısı.</summary>
        [HttpPut("{id:int}")]
        public Task<ActionResult<SistemDto>> Guncelle(int id, [FromBody] SistemGuncelleDto dto, CancellationToken ct = default)
            => Calistir(() => _service.GuncelleAsync(id, dto, ct));

        /// <summary>Rotadaki kayıt <c>HedefId</c>'ye taşınır ve silinir.</summary>
        [HttpPost("{id:int}/birlestir")]
        public Task<ActionResult<SistemBirlestirmeSonucuDto>> Birlestir(int id, [FromBody] SistemBirlestirDto dto,
                                                                       CancellationToken ct = default)
            => Calistir(() => _service.BirlestirAsync(id, dto.HedefId, ct));

        private async Task<ActionResult<T>> Calistir<T>(Func<Task<T>> is_)
        {
            try
            {
                return Ok(await is_());
            }
            catch (SistemBenzerException ex)
            {
                return Conflict(new SistemBenzerDto { Message = ex.Message, Benzer = ex.Benzer });
            }
            catch (SistemKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "id", message = ex.Message });
            }
        }
    }

    /// <summary>Anasayfa "Kullanılan sistemler" kartının formu. Kapsamı <see cref="BankaFirmaFiltresi"/> doğrular.</summary>
    [ApiController]
    [Route("api/catalog/anasayfa/firmalar/{firmaId:int}/sistemler")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class FirmaSistemleriController : ControllerBase
    {
        private readonly ISistemService _service;

        public FirmaSistemleriController(ISistemService service) => _service = service;

        [HttpPut]
        public async Task<IActionResult> Kaydet(int firmaId, [FromBody] FirmaSistemleriKaydetDto dto, CancellationToken ct = default)
        {
            try
            {
                await _service.FirmaSistemleriKaydetAsync(firmaId, dto, ct);
                return NoContent();
            }
            catch (SistemKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }
    }
}
