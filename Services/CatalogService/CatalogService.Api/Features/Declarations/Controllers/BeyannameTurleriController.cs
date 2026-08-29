using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Declarations.Controllers
{
    /// <summary>
    /// Beyanname türü tanımlarının tek kaynağı. Hem Takip sekmesinin açılır listesi hem
    /// Özet matrisinin kolonları buradan besleniyor; Takip'in kendi sabit listesi kaldırıldı.
    ///
    /// <c>GET</c> ucu eskiden <c>BeyannameOzetController</c> üzerindeydi; adresi
    /// (<c>api/catalog/beyanname/turler</c>) değişmedi, yalnız yazma uçlarıyla aynı
    /// controller'a taşındı.
    /// </summary>
    [ApiController]
    [Route("api/catalog/beyanname/turler")]
    [Authorize]
    public class BeyannameTurleriController : ControllerBase
    {
        private readonly IBeyannameTuruService _service;
        private readonly ILogger<BeyannameTurleriController> _logger;

        public BeyannameTurleriController(IBeyannameTuruService service,
                                          ILogger<BeyannameTurleriController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Tanımlar; <c>pasifDahil=true</c> ile pasifler de gelir (yönetim ekranı).</summary>
        [HttpGet]
        public async Task<ActionResult<List<BeyannameTuruDto>>> GetHepsi([FromQuery] bool pasifDahil = false,
                                                                         CancellationToken ct = default)
            => Ok(await _service.GetHepsiAsync(pasifDahil, ct));

        [HttpPost]
        public async Task<ActionResult<BeyannameTuruDto>> Create([FromBody] BeyannameTuruYazDto dto,
                                                                  CancellationToken ct = default)
        {
            try
            {
                return Ok(await _service.CreateAsync(dto, ct));
            }
            catch (BeyannameKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<BeyannameTuruDto>> Update(int id,
                                                                  [FromBody] BeyannameTuruYazDto dto,
                                                                  CancellationToken ct = default)
        {
            try
            {
                var kayit = await _service.UpdateAsync(id, dto, ct);
                return kayit is null ? NotFound() : Ok(kayit);
            }
            catch (BeyannameKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>
        /// Eksik varsayılan tanımları yükler ("Varsayılanları yükle" düğmesi).
        ///
        /// Açılış seed'i kurulu bir veritabanında herhangi bir sebeple çalışmamışsa
        /// (önceki bir seed adımının hatası, elle temizlenmiş tablo) kullanıcı yeni bir
        /// deploy beklemeden tabloyu doldurabilsin diye var — hesap planındaki
        /// "Tek düzen hesap planını yükle" ucuyla aynı kalıp (KARARLAR §84).
        ///
        /// Satır bazında idempotent: mevcut tanımların üzerine yazmaz, yalnız eksikleri ekler.
        /// </summary>
        [HttpPost("varsayilanlari-yukle")]
        public async Task<IActionResult> VarsayilanlariYukle(CancellationToken ct = default)
        {
            var (eklenen, toplam) = await _service.VarsayilanlariYukleAsync(ct);

            _logger.LogInformation("Beyanname türleri elle yüklendi: {Eklenen} eklendi, toplam {Toplam}.",
                                   eklenen, toplam);

            return Ok(new
            {
                eklenen,
                toplam,
                message = eklenen > 0
                    ? $"{eklenen} varsayılan tanım eklendi (toplam {toplam})."
                    : $"Eksik varsayılan tanım yoktu; tabloda {toplam} tanım var."
            });
        }
    }
}
