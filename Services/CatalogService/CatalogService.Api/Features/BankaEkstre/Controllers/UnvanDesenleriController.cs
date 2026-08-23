using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Unvan çıkarma desenleri. Banka bazlı, sıralı denenir, ilk yakalayan kazanır.
    /// <c>dene</c> uç noktası deseni kaydetmeden çalıştırır: kullanıcı bir ekstre
    /// açıklaması yapıştırıp desenin ne yakaladığını görebilir.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/unvan-desenleri")]
    [Authorize]
    public class UnvanDesenleriController : ControllerBase
    {
        private readonly IUnvanDeseniService _service;

        public UnvanDesenleriController(IUnvanDeseniService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<UnvanDeseniDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));

        /// <summary>
        /// Denemede geçersiz regex <b>hata döndürmez</b>, sonucun içinde bildirilir:
        /// kullanıcı deseni yazarken her tuşta 400 almasın.
        /// </summary>
        [HttpPost("dene")]
        public ActionResult<DesenDenemeSonucDto> Dene([FromBody] DesenDenemeIstegiDto istek)
            => Ok(_service.Dene(istek));

        [HttpPost]
        public async Task<ActionResult<UnvanDeseniDto>> Create([FromBody] UnvanDeseniYazDto dto, CancellationToken ct)
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
        public async Task<ActionResult<UnvanDeseniDto>> Update(int id, [FromBody] UnvanDeseniYazDto dto, CancellationToken ct)
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
