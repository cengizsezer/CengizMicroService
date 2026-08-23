using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Kişi → hesap yönlendirmeleri. Sabit kural grubu kişinin ne olduğunu bilmiyor:
    /// "masraf ödemesi" geçen her satırı 195'e yolluyor. Ortak ve yöneticiler için aynı
    /// ifade 331'e gitmeli; bu bilgi koda gömülmez, kullanıcı buradan tanımlar.
    ///
    /// Tablo firma bazlıdır ve katman sabit kuraldan <b>önce</b> çalışır.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/kisi-yonlendirmeleri")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class KisiYonlendirmeleriController : ControllerBase
    {
        private readonly IKisiYonlendirmeService _service;

        public KisiYonlendirmeleriController(IKisiYonlendirmeService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<KisiYonlendirmeDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));

        [HttpPost]
        public async Task<ActionResult<KisiYonlendirmeDto>> Create([FromBody] KisiYonlendirmeYazDto dto,
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
        public async Task<ActionResult<KisiYonlendirmeDto>> Update(int id,
                                                                   [FromBody] KisiYonlendirmeYazDto dto,
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
