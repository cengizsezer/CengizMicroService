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
    ///
    /// Firma kapsamı filtresi (<c>BankaFirmaFiltresi</c>) burada <b>uygulanmaz</b>: ekran
    /// firmaya girilmeden önce açılıyor ve doğası gereği birden çok firmayı soruyor.
    /// Eskisinden farkı, bunun artık bir baypas değil sıradan bir sorgu olması.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/firmalar")]
    [Authorize]
    public class FirmaOzetController : ControllerBase
    {
        private readonly IFirmaOzetService _service;

        public FirmaOzetController(IFirmaOzetService service) => _service = service;

        /// <summary>
        /// <c>?firmaIdler=3&amp;firmaIdler=7</c> — <c>catalog.Firmalar.Id</c> listesi.
        /// İstemci listeyi Raporlar'la aynı kaynaktan (<c>/catalog/firmalar</c>) kurar.
        /// Yalnız adet döner, kayıt içeriği dönmez.
        /// </summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<List<FirmaBankaOzetiDto>>> Ozet([FromQuery] int[] firmaIdler,
                                                                      CancellationToken ct)
            => Ok(await _service.OzetlerAsync(firmaIdler ?? Array.Empty<int>(), ct));
    }
}
