using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Muhasebe.Controllers
{
    /// <summary>
    /// TCMB EFT katılımcı kodları. Ortak referans veri; tenant'a bağlı değildir ve
    /// hesap planı seed dosyasıyla aynı kaynaktan (<c>tcmb-banka-kodlari.json</c>) okunur.
    /// </summary>
    [ApiController]
    [Route("api/catalog/muhasebe/banka-kodlari")]
    [Authorize]
    public class BankaKodlariController : ControllerBase
    {
        private readonly IBankaKoduService _service;

        public BankaKodlariController(IBankaKoduService service) => _service = service;

        /// <summary>Banka muavini açma diyaloğunun banka seçicisini besler.</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<BankaKoduDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));
    }
}
