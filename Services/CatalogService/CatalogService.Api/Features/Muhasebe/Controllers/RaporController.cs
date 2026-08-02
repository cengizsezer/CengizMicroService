using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Muhasebe.Controllers
{
    /// <summary>
    /// Muhasebe raporları: mizan, T cetveli (ekstre) ve masraf merkezi dağılımı.
    /// FirmaId (TenantNo) token'dan gelir; tenant izolasyonu <c>CatalogContext</c>
    /// query filter'ları ile sağlanır. Uçların tamamı salt okunurdur.
    /// </summary>
    [ApiController]
    [Route("api/catalog/muhasebe/rapor")]
    [Authorize]
    public class RaporController : ControllerBase
    {
        private readonly IRaporService _service;

        public RaporController(IRaporService service) => _service = service;

        /// <summary>Mizan. <c>seviye</c> verilirse o seviyeye kadar olan hesaplar döner (3 = kebir).</summary>
        [HttpGet("mizan")]
        public async Task<ActionResult<MizanDto>> GetMizan(
            [FromQuery] DateTime? bas,
            [FromQuery] DateTime? bit,
            [FromQuery] byte? seviye,
            CancellationToken ct)
            => Ok(await _service.GetMizanAsync(new RaporFiltreDto { Bas = bas, Bit = bit }, seviye, ct));

        /// <summary>T cetveli verisi. Üst hesap seçilirse alt ağacının hareketleri toplanır.</summary>
        [HttpGet("ekstre/{hesapId:int}")]
        public async Task<ActionResult<EkstreDto>> GetEkstre(
            int hesapId,
            [FromQuery] DateTime? bas,
            [FromQuery] DateTime? bit,
            CancellationToken ct)
        {
            var dto = await _service.GetEkstreAsync(hesapId, new RaporFiltreDto { Bas = bas, Bit = bit }, ct);
            return dto is null ? NotFound(new { message = "Hesap bulunamadı." }) : Ok(dto);
        }

        /// <summary>Masraf merkezi dağılımı ve hesap kırılımı.</summary>
        [HttpGet("masraf-merkezi")]
        public async Task<ActionResult<MasrafMerkeziRaporDto>> GetMasrafMerkezi(
            [FromQuery] DateTime? bas,
            [FromQuery] DateTime? bit,
            CancellationToken ct)
            => Ok(await _service.GetMasrafMerkeziAsync(new RaporFiltreDto { Bas = bas, Bit = bit }, ct));
    }
}
