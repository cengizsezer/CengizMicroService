using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Muhasebe.Controllers
{
    /// <summary>
    /// Masraf merkezi tanımları. FirmaId (TenantNo) token'dan gelir; tenant izolasyonu
    /// <c>CatalogContext</c> query filter'ı ile sağlanır. Fiş giriş ekranının masraf
    /// merkezi seçicisini besler; daha önce bu liste rapor ucundan türetiliyordu.
    /// </summary>
    [ApiController]
    [Route("api/catalog/muhasebe/masraf-merkezi")]
    [Authorize]
    public class MasrafMerkeziController : ControllerBase
    {
        private readonly IMasrafMerkeziService _service;

        public MasrafMerkeziController(IMasrafMerkeziService service) => _service = service;

        /// <summary>Varsayılan olarak yalnızca aktif merkezler; geçmiş kayıtlar için <c>pasifDahil=true</c>.</summary>
        [HttpGet]
        public async Task<ActionResult<List<MasrafMerkeziDto>>> GetHepsi([FromQuery] bool pasifDahil, CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(pasifDahil, ct));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MasrafMerkeziDto>> GetById(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<MasrafMerkeziDto>> Create([FromBody] MasrafMerkeziYazDto dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.CreateAsync(dto, ct);
                return CreatedAtAction(nameof(GetById), new { id = created.MasrafMerkeziId }, created);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
            catch (MuhasebeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Silme yoktur; pasif merkez yeni fişlerde seçilemez, geçmiş raporlarda görünür.</summary>
        [HttpPatch("{id:int}/pasif")]
        public async Task<ActionResult<MasrafMerkeziDto>> PasifeAl(int id, CancellationToken ct)
        {
            var dto = await _service.PasifeAlAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
    }
}
