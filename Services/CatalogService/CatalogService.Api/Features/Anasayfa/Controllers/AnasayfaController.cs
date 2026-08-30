using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
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
        private readonly IFirmaPaneliService _panel;
        private readonly IBankaFirmaKapsami _kapsam;

        public AnasayfaController(IAnasayfaService service, IFirmaPaneliService panel,
                                  IBankaFirmaKapsami kapsam)
        {
            _service = service;
            _panel = panel;
            _kapsam = kapsam;
        }

        /// <summary>Dönem verilmezse içinde bulunulan ay kullanılır.</summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<AnasayfaOzetDto>> Ozet([FromQuery] int? yil, [FromQuery] int? ay,
                                                              CancellationToken ct = default)
            => Ok(await _service.OzetAsync(yil ?? DateTime.Today.Year, ay ?? DateTime.Today.Month, ct));

        /// <summary>
        /// Firma bilgi paneli: <b>tüm</b> firmaların liste satırları (uyarılarıyla) ve
        /// seçili firmanın ayrıntısı — tek çağrıda. Ekran açılırken firma başına ayrı
        /// istek atılmıyor.
        ///
        /// Seçili firma <c>?firmaId=</c> ile geliyor; kapsamı Banka Otomasyon'daki
        /// mekanizmanın aynısı olan <see cref="BankaFirmaFiltresi"/> kuruyor. Parametre
        /// yoksa sunucu ilk firmayı seçer (ilk açılışta sağ panel boş kalmasın);
        /// tanınmayan firma değeri filtreden 400 döner — sessizce başka bir firmanın
        /// bilgisi gösterilmez.
        /// </summary>
        [HttpGet("firma-paneli")]
        [ServiceFilter(typeof(BankaFirmaFiltresi))]
        public async Task<ActionResult<FirmaPaneliDto>> FirmaPaneli(CancellationToken ct = default)
            => Ok(await _panel.PanelAsync(_kapsam.Secili ? _kapsam.FirmaId : null, ct));
    }
}
