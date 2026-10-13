using CatalogService.Api.Features.Parolalar.Dtos;
using CatalogService.Api.Features.Parolalar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Parolalar.Controllers
{
    /// <summary>
    /// Yönetim › Parolalar. Rota <c>api/catalog/parolalar</c>; gateway'in genel <c>/catalog/{everything}</c> rotası yeterli.
    ///
    /// Liste ŞİFRE DÖNDÜRMEZ (yalnız dolu/boş). Şifre yalnız tek alan için <c>GET {id}/alan/{alan}?islem=</c> ile gelir;
    /// her çağrı erişim kaydına yazılır ve yanıt önbelleğe alınmaz. Bu controller hiçbir şeyi loglamaz.
    ///
    /// Yetki TÜM uçlarda sunucuda: <see cref="ParolaYetkisi.Politika"/> (insan + pkf rolü). Rolü olmayan 403 alır ve
    /// bu erişim kaydına "reddedildi" olarak yazılır (<see cref="ParolaReddiKaydedici"/>). Kapsam yok: Firmalarım global.
    /// </summary>
    [ApiController]
    [Route("api/catalog/parolalar")]
    [Authorize(Policy = ParolaYetkisi.Politika)]
    public class ParolalarController : ControllerBase
    {
        private readonly IParolaService _service;

        public ParolalarController(IParolaService service) => _service = service;

        [HttpGet]
        public Task<ActionResult<List<ParolaDto>>> Liste(CancellationToken ct = default)
            => Calistir(() => _service.ListeAsync(ct));

        [HttpPost]
        public Task<ActionResult<ParolaDto>> Ekle([FromBody] ParolaKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkleAsync(dto, ct));

        [HttpPut("{id:int}")]
        public Task<ActionResult<ParolaDto>> Guncelle(int id, [FromBody] ParolaKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.GuncelleAsync(id, dto, ct));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Sil(int id, CancellationToken ct = default)
        {
            try
            {
                await _service.SilAsync(id, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "id", message = ex.Message });
            }
        }

        /// <summary>Tek şifre alanı: <c>alan</c> = sifre | parola | sistemSifresi | isyeriSifresi; <c>islem</c> = goruntule | kopyala.</summary>
        [HttpGet("{id:int}/alan/{alan}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public Task<ActionResult<ParolaAlanDegeriDto>> Alan(int id, string alan, [FromQuery] string? islem, CancellationToken ct = default)
            => Calistir(() => _service.AlanAsync(id, alan, islem, ct));

        private async Task<ActionResult<T>> Calistir<T>(Func<Task<T>> is_)
        {
            try
            {
                return Ok(await is_());
            }
            catch (ParolaKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (ParolaCakismaException ex)
            {
                return Conflict(new { field = "sube", message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "id", message = ex.Message });
            }
            catch (ParolaCozulemediException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { field = "alan", message = ex.Message });
            }
        }
    }
}
