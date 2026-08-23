using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Vergi kodu → hesap eşleme tablosu. Vergi tahsilatı satırlarında karşı hesap metnin
    /// içeriğine göre değiştiği için tek kural yetmiyor; tablo Tanımlar ekranından
    /// düzenlenir, kod değişmez.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/vergi-kodlari")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class VergiKodlariController : ControllerBase
    {
        private readonly IVergiKoduService _service;

        public VergiKodlariController(IVergiKoduService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<VergiKoduEslemesiDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));

        [HttpPost]
        public async Task<ActionResult<VergiKoduEslemesiDto>> Create([FromBody] VergiKoduEslemesiYazDto dto,
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
        public async Task<ActionResult<VergiKoduEslemesiDto>> Update(int id,
                                                                    [FromBody] VergiKoduEslemesiYazDto dto,
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
