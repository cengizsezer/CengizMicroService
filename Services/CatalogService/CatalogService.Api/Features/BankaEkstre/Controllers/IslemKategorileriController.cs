using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// İşlem kategorileri: kuralların muhasebe sınıflandırması. Kategori yalnız etiket ve
    /// görünüm; eşleştirme kararına girmez.
    ///
    /// <c>kapsam</c> ucu, bir bankanın kurallarını kategorilere dağıtılmış hâlde verir —
    /// yeni banka eklerken eksik kategorileri görmek için kontrol listesi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/islem-kategorileri")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class IslemKategorileriController : ControllerBase
    {
        private readonly IIslemKategorisiService _service;

        public IslemKategorileriController(IIslemKategorisiService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<IslemKategorisiDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));

        /// <param name="parserTipi">
        /// Bankanın ayrıştırıcısı. Boş bırakılırsa yalnız tüm bankalarda geçerli kayıtlar
        /// (ve global/firma tabloları) sayılır — bankanın hiçbir hesabında ayrıştırıcı
        /// seçili değilken ekranın gördüğü kümenin aynısı.
        /// </param>
        [HttpGet("kapsam")]
        public async Task<ActionResult<KategoriKapsamOzetiDto>> Kapsam([FromQuery] string? parserTipi,
                                                                      CancellationToken ct)
            => Ok(await _service.KapsamAsync(parserTipi, ct));

        [HttpPost]
        public async Task<ActionResult<IslemKategorisiDto>> Create([FromBody] IslemKategorisiYazDto dto,
                                                                   CancellationToken ct)
        {
            try
            {
                return Ok(await _service.CreateAsync(dto, ct));
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<IslemKategorisiDto>> Update(int id,
                                                                   [FromBody] IslemKategorisiYazDto dto,
                                                                   CancellationToken ct)
        {
            try
            {
                var kayit = await _service.UpdateAsync(id, dto, ct);
                return kayit is null ? NotFound() : Ok(kayit);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
            => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }
}
