using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Ekstresi işlenen banka hesapları. Rota <c>api/catalog/*</c> altında olduğu için
    /// gateway'in mevcut <c>/catalog/{everything}</c> route'undan değişiklik olmadan geçer.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/banka-hesaplari")]
    [Authorize]
    public class BankaHesaplariController : ControllerBase
    {
        private readonly IBankaHesabiService _service;

        public BankaHesaplariController(IBankaHesabiService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<BankaHesabiDto>>> GetHepsi([FromQuery] bool pasifDahil = false,
                                                                      CancellationToken ct = default)
            => Ok(await _service.GetHepsiAsync(pasifDahil, ct));

        /// <summary>Hesap tanımlarken seçilebilecek ayrıştırıcılar.</summary>
        [HttpGet("parserler")]
        public ActionResult<List<ParserSecenekDto>> GetParserler()
            => Ok(_service.GetParserSecenekleri());

        [HttpGet("{id:int}")]
        public async Task<ActionResult<BankaHesabiDto>> GetById(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BankaHesabiDto>> Create([FromBody] BankaHesabiYazDto dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.CreateAsync(dto, ct);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<BankaHesabiDto>> Update(int id, [FromBody] BankaHesabiYazDto dto, CancellationToken ct)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto, ct);
                return updated is null ? NotFound() : Ok(updated);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Ekstresi olan hesap silinemez; kullanılmayacaksa pasife alınır.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var sonuc = await _service.DeleteAsync(id, ct);
            return sonuc switch
            {
                true => NoContent(),
                false => Conflict(new { message = "Bu hesaba ait ekstre yüklemesi var; hesap silinemez. Hesabı pasife alın." }),
                _ => NotFound()
            };
        }
    }
}
