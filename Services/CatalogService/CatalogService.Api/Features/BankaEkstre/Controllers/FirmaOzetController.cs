using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Banka Otomasyon firma seçim ekranını besleyen sayaçlar. Rota yine
    /// <c>api/catalog/*</c> altında; gateway'in <c>/catalog/{everything}</c> route'u
    /// değişmeden geçirir.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/firmalar")]
    [Authorize]
    public class FirmaOzetController : ControllerBase
    {
        private readonly IFirmaOzetService _service;

        public FirmaOzetController(IFirmaOzetService service) => _service = service;

        /// <summary>
        /// <c>?tenantlar=201&amp;tenantlar=106</c>. İstemci listeyi login yanıtındaki kendi
        /// firmalarından kurar; sunucu token'da tek tenant gördüğü için doğrulayamaz,
        /// bu yüzden yalnız adet döner (bkz. <see cref="FirmaOzetService"/>).
        /// </summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<List<FirmaBankaOzetiDto>>> Ozet([FromQuery] string[] tenantlar,
                                                                      CancellationToken ct)
            => Ok(await _service.OzetlerAsync(tenantlar ?? Array.Empty<string>(), ct));
    }
}
