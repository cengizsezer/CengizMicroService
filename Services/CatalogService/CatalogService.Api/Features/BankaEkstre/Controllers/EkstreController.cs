using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>Ekstre yükleme, satır onayı ve ORKA dışa aktarımı.</summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/ekstre")]
    [Authorize]
    public class EkstreController : ControllerBase
    {
        private const long EnFazlaDosyaBayt = 20 * 1024 * 1024;

        private readonly IEkstreService _service;

        public EkstreController(IEkstreService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<EkstreYuklemeDto>>> GetYuklemeler(CancellationToken ct)
            => Ok(await _service.GetYuklemelerAsync(ct));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<EkstreYuklemeDto>> GetYukleme(int id, CancellationToken ct)
        {
            var dto = await _service.GetYuklemeAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        /// <summary>Ekstre yükler ve satırları anında işler (açıklama + katmanlı eşleştirme).</summary>
        [HttpPost("yukle")]
        [RequestSizeLimit(EnFazlaDosyaBayt)]
        public async Task<ActionResult<EkstreYuklemeDto>> Yukle(
            [FromForm] int bankaHesabiId,
            IFormFile file,
            CancellationToken ct = default)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Boş dosya." });

            if (file.Length > EnFazlaDosyaBayt)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "Dosya boyutu 20 MB sınırını aşıyor." });

            var uzanti = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (uzanti != ".xlsx" && uzanti != ".xls")
                return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                    new { message = "Sadece .xlsx veya .xls dosyaları desteklenir." });

            try
            {
                // ClosedXML akışta ileri-geri konumlanabilmek istiyor; belleğe alınır.
                using var bellek = new MemoryStream();
                await using (var kaynak = file.OpenReadStream())
                    await kaynak.CopyToAsync(bellek, ct);
                bellek.Position = 0;

                var sonuc = await _service.YukleAsync(bankaHesabiId, bellek, file.FileName, ct);
                return Ok(sonuc);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex.GetType().FullName?.Contains("ClosedXML", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                    new { message = $"Excel dosyası okunamadı: {ex.Message}" });
            }
        }

        /// <summary>Satırlar; <c>durum</c> ile filtrelenir (onay ekranı varsayılan olarak OnayBekliyor kullanır).</summary>
        [HttpGet("{id:int}/satirlar")]
        public async Task<ActionResult<List<EkstreSatirDto>>> GetSatirlar(int id, [FromQuery] SatirDurum? durum,
                                                                         CancellationToken ct = default)
        {
            var satirlar = await _service.GetSatirlarAsync(id, durum, ct);
            return satirlar is null ? NotFound() : Ok(satirlar);
        }

        [HttpPut("satir/{satirId:int}/onayla")]
        public async Task<ActionResult<EkstreSatirDto>> Onayla(int satirId, [FromBody] SatirOnaylaDto dto,
                                                              CancellationToken ct = default)
        {
            try
            {
                var satir = await _service.OnaylaAsync(satirId, dto.HesapKodu, dto.KisiYonlendir, ct);
                return satir is null ? NotFound() : Ok(satir);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Karşı bacağı başka bankanın ekstresinde işlenmiş satır; dışa aktarımdan düşer.</summary>
        [HttpPut("satir/{satirId:int}/diger-bankada")]
        public async Task<ActionResult<EkstreSatirDto>> DigerBankada(int satirId, CancellationToken ct)
        {
            var satir = await _service.DigerBankadaAsync(satirId, ct);
            return satir is null ? NotFound() : Ok(satir);
        }

        /// <summary>
        /// Dışa aktarımın ikinci parçası: ORKA gridine yazılacak karşı hesap kodu listesi.
        /// Çözülemeyen veya onay bekleyen satır varsa 400 döner.
        /// </summary>
        [HttpPost("{id:int}/disa-aktar")]
        public async Task<ActionResult<DisaAktarimSonucDto>> DisaAktar(int id, CancellationToken ct)
        {
            try
            {
                var sonuc = await _service.DisaAktarAsync(id, ct);
                return sonuc is null ? NotFound() : Ok(sonuc);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>
        /// Dışa aktarımın birinci parçası: orijinal ekstre yapısında, açıklama kolonu
        /// üretilen açıklamayla değiştirilmiş dosya. Değiştirilmezse ORKA gridinde ham
        /// banka metni görünür.
        /// </summary>
        [HttpPost("{id:int}/duzeltilmis-ekstre")]
        public async Task<IActionResult> DuzeltilmisEkstre(int id, CancellationToken ct)
        {
            try
            {
                var dosya = await _service.DuzeltilmisEkstreAsync(id, ct);
                if (dosya is null) return NotFound();

                return File(dosya.Icerik,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            dosya.DosyaAdi);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>
        /// Analiz dökümü: satırların tamamı, durumu ne olursa olsun. "Kod listesi" ve
        /// "Düzeltilmiş ekstre" eksik satır varken 400 dönmeye devam eder — bu döküm ise
        /// sistemin ne önerdiğini onaydan önce incelemek için, ORKA'ya yüklenmez.
        /// </summary>
        [HttpPost("{id:int}/analiz-dokumu")]
        public async Task<IActionResult> AnalizDokumu(int id, CancellationToken ct)
        {
            var dosya = await _service.AnalizDokumuAsync(id, ct);
            if (dosya is null) return NotFound();

            return File(dosya.Icerik,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        dosya.DosyaAdi);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Sil(int id, CancellationToken ct)
            => await _service.SilAsync(id, ct) ? NoContent() : NotFound();
    }
}
