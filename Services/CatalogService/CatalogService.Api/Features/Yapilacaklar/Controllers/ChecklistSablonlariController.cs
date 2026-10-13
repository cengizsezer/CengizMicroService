using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Yapilacaklar.Controllers
{
    /// <summary>
    /// Yönetim → Checklist Şablonları (Firma Panosu Aşama 3). Global; rota <c>api/catalog/*</c>, gateway değişmedi.
    /// Kullanılmış şablon silinmez, pasife alınır (DELETE 200 + <c>pasifeAlindi</c>). Şablonu değiştirmek oluşmuş
    /// işleri değiştirmez. Uygulama uçları <see cref="FirmaPanosuController"/>'da (<c>isler/sablondan</c>).
    /// </summary>
    [ApiController]
    [Route("api/catalog/firma-panosu/sablonlar")]
    [Authorize]
    public class ChecklistSablonlariController : ControllerBase
    {
        private readonly IChecklistSablonService _service;

        public ChecklistSablonlariController(IChecklistSablonService service) => _service = service;

        [HttpGet]
        public Task<ActionResult<List<ChecklistSablonuDto>>> Liste(CancellationToken ct = default)
            => Calistir(() => _service.ListeAsync(ct));

        [HttpGet("{id:int}")]
        public Task<ActionResult<ChecklistSablonuDto>> Getir(int id, CancellationToken ct = default)
            => Calistir(() => _service.GetirAsync(id, ct));

        [HttpPost]
        public Task<ActionResult<ChecklistSablonuDto>> Ekle([FromBody] ChecklistSablonuKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkleAsync(dto, ct));

        [HttpPut("{id:int}")]
        public Task<ActionResult<ChecklistSablonuDto>> Guncelle(int id, [FromBody] ChecklistSablonuKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.GuncelleAsync(id, dto, ct));

        [HttpDelete("{id:int}")]
        public Task<ActionResult<ChecklistSablonSilSonucDto>> Sil(int id, CancellationToken ct = default)
            => Calistir(() => _service.SilAsync(id, ct));

        /// <summary>Firmanın özel işlerinden şablona girecek maddeler (yazmaz).</summary>
        [HttpGet("firmadan/onizle")]
        public Task<ActionResult<FirmadanSablonOnizlemeDto>> FirmadanOnizle([FromQuery] int firma, CancellationToken ct = default)
            => Calistir(() => _service.FirmadanOnizleAsync(firma, ct));

        [HttpPost("firmadan")]
        public Task<ActionResult<ChecklistSablonuDto>> Firmadan([FromBody] FirmadanSablonDto dto, CancellationToken ct = default)
            => Calistir(() => _service.FirmadanOlusturAsync(dto, ct));

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
