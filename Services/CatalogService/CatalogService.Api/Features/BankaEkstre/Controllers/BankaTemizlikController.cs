using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// "Bu firmanın banka otomasyon verisini temizle" (Tanımlar &gt; Veri temizliği).
    ///
    /// Silme <c>DELETE</c> ile yapılır ve <b>önce</b> özet okunur: ekran hangi tablodan
    /// kaç kayıt gideceğini yazıp onay ister. Silinen veri geri gelmiyor; sayıları
    /// göstermeden onay istemek kullanıcıyı kör imzaya zorlardı.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/temizlik")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class BankaTemizlikController : ControllerBase
    {
        private readonly IBankaTemizlikService _service;

        public BankaTemizlikController(IBankaTemizlikService service) => _service = service;

        /// <summary>Seçili firmada silinecek kayıt sayıları.</summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<BankaTemizlikOzetiDto>> Ozet(CancellationToken ct)
            => Ok(await _service.OzetAsync(ct));

        [HttpDelete]
        public async Task<ActionResult<BankaTemizlikOzetiDto>> Temizle(CancellationToken ct)
            => Ok(await _service.TemizleAsync(ct));

        /// <summary>
        /// Hiçbir firmaya bağlı olmayan (<c>FirmaId = 0</c>) eski kayıtlar. Modülün tenant
        /// bazlı olduğu dönemden kalıyorlar; hiçbir firmanın ekranında görünmedikleri için
        /// başka türlü silinemezler (bkz. KARARLAR §71).
        /// </summary>
        [HttpGet("sahipsiz/ozet")]
        public async Task<ActionResult<BankaTemizlikOzetiDto>> SahipsizOzet(CancellationToken ct)
            => Ok(await _service.SahipsizOzetAsync(ct));

        [HttpDelete("sahipsiz")]
        public async Task<ActionResult<BankaTemizlikOzetiDto>> SahipsizTemizle(CancellationToken ct)
            => Ok(await _service.SahipsizTemizleAsync(ct));
    }
}
