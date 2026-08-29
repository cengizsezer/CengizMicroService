using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Anasayfa.Controllers
{
    /// <summary>
    /// Anasayfa kartları. Rota <c>api/catalog/*</c> altında olduğu için gateway
    /// değişmedi.
    ///
    /// Firma kapsam filtresi yok: anasayfa doğası gereği tüm firmaları birden gösteriyor
    /// (Banka Otomasyon firma seçim ekranıyla aynı gerekçe).
    /// </summary>
    [ApiController]
    [Route("api/catalog/anasayfa")]
    [Authorize]
    public class AnasayfaController : ControllerBase
    {
        private readonly IAnasayfaService _service;

        public AnasayfaController(IAnasayfaService service) => _service = service;

        /// <summary>Dönem verilmezse içinde bulunulan ay kullanılır.</summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<AnasayfaOzetDto>> Ozet([FromQuery] int? yil, [FromQuery] int? ay,
                                                              CancellationToken ct = default)
            => Ok(await _service.OzetAsync(yil ?? DateTime.Today.Year, ay ?? DateTime.Today.Month, ct));
    }
}
