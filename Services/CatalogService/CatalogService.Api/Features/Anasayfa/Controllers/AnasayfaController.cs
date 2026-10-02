using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Infrastructure.Auth;
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
        private readonly IFirmaKunyeService _kunye;
        private readonly IHttpCurrentUser _kullanici;

        public AnasayfaController(IAnasayfaService service, IFirmaPaneliService panel,
                                  IBankaFirmaKapsami kapsam, IFirmaKunyeService kunye,
                                  IHttpCurrentUser kullanici)
        {
            _service = service;
            _panel = panel;
            _kapsam = kapsam;
            _kunye = kunye;
            _kullanici = kullanici;
        }

        /// <summary>Dönem verilmezse içinde bulunulan ay kullanılır.</summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<AnasayfaOzetDto>> Ozet([FromQuery] int? yil, [FromQuery] int? ay,
                                                              CancellationToken ct = default)
            => Ok(await _service.OzetAsync(yil ?? 0, ay ?? 0, ct));   // boş = bugünün ayı (serviste, enjekte saatle)

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
            => Ok(await _panel.PanelAsync(_kapsam.Secili ? _kapsam.FirmaId : null, ct, KullaniciId()));

        // ---- Künye yazmaları: firma rotadaki kayıttan, filtre doğrular ----

        /// <summary>Sınıflandırma kartının "Düzenle" formu.</summary>
        [HttpPut("firmalar/{firmaId:int}/siniflandirma")]
        [ServiceFilter(typeof(BankaFirmaFiltresi))]
        public Task<IActionResult> SiniflandirmaKaydet(int firmaId, [FromBody] FirmaSiniflandirmaKaydetDto dto,
                                                        CancellationToken ct = default)
            => Calistir(() => _kunye.SiniflandirmaKaydetAsync(firmaId, dto, ct));

        // ---- Takip kartı ----

        /// <summary>Sorumlu ata / değiştir; <c>KullaniciId = null</c> sorumluyu kaldırır.</summary>
        [HttpPut("firmalar/{firmaId:int}/sorumlu")]
        [ServiceFilter(typeof(BankaFirmaFiltresi))]
        public Task<IActionResult> SorumluAta(int firmaId, [FromBody] FirmaSorumluAtaDto dto,
                                              CancellationToken ct = default)
            => Calistir(() => _kunye.SorumluAtaAsync(firmaId, dto, ct));

        [HttpGet("firmalar/{firmaId:int}/notlar")]
        [ServiceFilter(typeof(BankaFirmaFiltresi))]
        public async Task<ActionResult<List<FirmaNotuDto>>> Notlar(int firmaId, CancellationToken ct = default)
            => Ok(await _kunye.NotlarAsync(firmaId, ct));

        [HttpPost("firmalar/{firmaId:int}/notlar")]
        [ServiceFilter(typeof(BankaFirmaFiltresi))]
        public async Task<ActionResult<FirmaNotuDto>> NotEkle(int firmaId, [FromBody] FirmaNotuEkleDto dto,
                                                              CancellationToken ct = default)
        {
            try
            {
                return Ok(await _kunye.NotEkleAsync(firmaId, dto, ct));
            }
            catch (FirmaKunyeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Yalnız notu yazan silebilir; başkasının notu 403.</summary>
        [HttpDelete("firmalar/{firmaId:int}/notlar/{notId:long}")]
        [ServiceFilter(typeof(BankaFirmaFiltresi))]
        public async Task<IActionResult> NotSil(int firmaId, long notId, CancellationToken ct = default)
        {
            try
            {
                await _kunye.NotSilAsync(firmaId, notId, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "notId", message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { field = "notId", message = ex.Message });
            }
        }

        /// <summary>"Tüm hareketler": firmanın bütün olay kaydı, yeni → eski.</summary>
        [HttpGet("firmalar/{firmaId:int}/olaylar")]
        [ServiceFilter(typeof(BankaFirmaFiltresi))]
        public async Task<ActionResult<List<FirmaOlayDto>>> Olaylar(int firmaId, CancellationToken ct = default)
            => Ok(await _kunye.OlaylarAsync(firmaId, ct));

        private string? KullaniciId()
        {
            try
            {
                return _kullanici.IsAuthenticated ? _kullanici.UserId : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Seçili mizan formatının boş şablonu (yalnız başlık satırı). Firma verisi
        /// okunmuyor; kapsam gerekmez.
        /// </summary>
        [HttpGet("mizan-sablonu")]
        public IActionResult MizanSablonu([FromQuery] string? format)
        {
            try
            {
                return File(FirmaKunyeService.MizanSablonu(format ?? string.Empty),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "mizan-sablonu.xlsx");
            }
            catch (FirmaKunyeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        private async Task<IActionResult> Calistir(Func<Task> is_)
        {
            try
            {
                await is_();
                return NoContent();
            }
            catch (FirmaKunyeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }
    }
}
