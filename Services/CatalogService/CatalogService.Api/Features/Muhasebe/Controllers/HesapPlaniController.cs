using CatalogService.Api.Features.Muhasebe;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Muhasebe.Controllers
{
    /// <summary>
    /// Hesap planı uçları. FirmaId (TenantNo) token'dan gelir, request body'sinden değil;
    /// tenant izolasyonu <c>CatalogContext</c> query filter'ları ile sağlanır.
    /// İş kuralları servistedir; buradaki kontroller yalnızca yönlendirme içindir.
    /// </summary>
    [ApiController]
    [Route("api/catalog/muhasebe/hesap-plani")]
    [Authorize]
    public class HesapPlaniController : ControllerBase
    {
        private readonly IHesapPlaniService _service;
        private readonly ITekDuzenPlanKaynagi _planKaynagi;
        private readonly ILogger<HesapPlaniController> _logger;

        public HesapPlaniController(IHesapPlaniService service,
                                    ITekDuzenPlanKaynagi planKaynagi,
                                    ILogger<HesapPlaniController> logger)
        {
            _service = service;
            _planKaynagi = planKaynagi;
            _logger = logger;
        }

        // ---- Kurulum ----

        /// <summary>
        /// Geçerli firmaya tekdüzen hesap planını yükler ("Tek düzen hesap planını yükle"
        /// düğmesi). Hangi firmaya yazılacağı istekten gelir: TenantNo'yu context damgalar.
        ///
        /// Açılıştaki seed yalnız <c>Program.cs</c>'teki sabit tenant listesi için
        /// çalışıyordu; listede olmayan firma planssız kalıyor ve ekranda boş sayfa olarak
        /// görünüyordu (KARARLAR §83). Bu uç, kullanıcının o an bağlı olduğu firmaya
        /// kontrollü biçimde plan yüklemeyi sağlar (§84).
        /// </summary>
        [HttpPost("tek-duzen-yukle")]
        public async Task<IActionResult> TekDuzenYukle(CancellationToken ct)
        {
            var (sonuc, adet) = await _service.TekDuzenPlaniYukleAsync(ct);

            switch (sonuc)
            {
                case PlanYuklemeSonuc.Yuklendi:
                    return Ok(new { adet, message = $"Tekdüzen hesap planı yüklendi ({adet} hesap)." });

                case PlanYuklemeSonuc.ZatenDolu:
                    return Conflict(new { message = "Bu firmanın hesap planı zaten dolu; yükleme yapılmadı." });

                default:
                    // Sessizce geçilmez: yayında eksik dosya sunucu logunda da görünsün.
                    _logger.LogError(
                        "Tekdüzen hesap planı şablonu bulunamadı: {Yol}. Yayına dosya eklenmeli.",
                        _planKaynagi.Yol);

                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = "Tekdüzen hesap planı şablonu sunucuda bulunamadı " +
                                  $"({DosyadanTekDuzenPlanKaynagi.DosyaAdi}). Yükleme yapılamadı; " +
                                  "sunucu yöneticisine bildirin."
                    });
            }
        }

        // ---- Okuma ----

        /// <summary>Ağacın tamamı; düz liste + Yol.</summary>
        [HttpGet]
        public async Task<ActionResult<List<HesapPlaniDto>>> GetHepsi(CancellationToken ct)
            => Ok(await _service.GetHepsiAsync(ct));

        /// <summary>Kod veya isimde arama.</summary>
        [HttpGet("ara")]
        public async Task<ActionResult<List<HesapPlaniDto>>> Ara([FromQuery] string? q, CancellationToken ct)
            => Ok(await _service.AraAsync(q, ct));

        /// <summary>Fiş girişi seçim listesi.</summary>
        [HttpGet("hareket-gorenler")]
        public async Task<ActionResult<List<HesapPlaniDto>>> GetHareketGorenler(CancellationToken ct)
            => Ok(await _service.GetHareketGorenlerAsync(ct));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<HesapPlaniDto>> GetById(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        /// <summary>Üst hesabın altındaki ilk boş segment.</summary>
        [HttpGet("{ustId:int}/sonraki-kod")]
        public async Task<ActionResult<SonrakiKodDto>> GetSonrakiKod(int ustId, CancellationToken ct)
        {
            try
            {
                var dto = await _service.GetSonrakiKodAsync(ustId, ct);
                return dto is null ? NotFound(new { message = "Üst hesap bulunamadı." }) : Ok(dto);
            }
            catch (MuhasebeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Grubun altındaki kullanılmamış kebir kodları.</summary>
        [HttpGet("{grupId:int}/bos-kebirler")]
        public async Task<ActionResult<List<BosKebirDto>>> GetBosKebirler(int grupId, CancellationToken ct)
        {
            try
            {
                var liste = await _service.GetBosKebirlerAsync(grupId, ct);
                return liste is null ? NotFound(new { message = "Grup hesabı bulunamadı." }) : Ok(liste);
            }
            catch (MuhasebeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        // ---- Yazma ----

        [HttpPost]
        public async Task<ActionResult<HesapPlaniDto>> Create([FromBody] HesapPlaniCreateDto dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.CreateAsync(dto, ct);
                return created is null
                    ? NotFound(new { message = "Üst hesap bulunamadı." })
                    : CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
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

        [HttpPut("{id:int}")]
        public async Task<ActionResult<HesapPlaniDto>> Update(int id, [FromBody] HesapPlaniUpdateDto dto, CancellationToken ct)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto, ct);
                return updated is null ? NotFound() : Ok(updated);
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

        /// <summary>Hesabı ve alt ağacını pasife çeker; pasif hesap yeni fişlerde seçilemez.</summary>
        [HttpPatch("{id:int}/pasif")]
        public async Task<ActionResult<HesapPlaniDto>> PasifeAl(int id, CancellationToken ct)
        {
            var dto = await _service.PasifeAlAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        /// <summary>Yalnızca hareketsiz, alt hesabı olmayan kullanıcı hesabı silinebilir.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var sonuc = await _service.DeleteAsync(id, ct);
            return sonuc switch
            {
                HesapSilmeSonuc.Silindi => NoContent(),
                HesapSilmeSonuc.SistemHesabi => Conflict(new { message = "Standart hesaplar silinemez. Kullanmayacaksanız hesabı pasife alın." }),
                HesapSilmeSonuc.AltHesapVar => Conflict(new { message = "Alt hesabı olan bir hesap silinemez. Önce alt hesapları silin." }),
                HesapSilmeSonuc.HareketVar => Conflict(new { message = "Hareketi olan hesap silinemez. Hesabı pasife alın; geçmiş raporlarda görünmeye devam eder." }),
                _ => NotFound()
            };
        }
    }
}
