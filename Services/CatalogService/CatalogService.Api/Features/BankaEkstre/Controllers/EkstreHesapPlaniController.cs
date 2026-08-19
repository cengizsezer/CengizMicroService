using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Banka ekstresi eşleştirmesinde kullanılan ORKA hesap planı. Muhasebe modülünün
    /// ağaç yapılı hesap planından ayrıdır (rota da ayrı): buradaki kodlar ORKA
    /// formatında, boşluklu ve harf içerebilir.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/hesap-plani")]
    [Authorize]
    public class EkstreHesapPlaniController : ControllerBase
    {
        private const long EnFazlaDosyaBayt = 20 * 1024 * 1024;

        private readonly IEkstreHesapPlaniService _service;

        public EkstreHesapPlaniController(IEkstreHesapPlaniService service) => _service = service;

        /// <summary>Kod/ad araması. Onay ekranındaki kod kutusu yazdıkça buradan öneri alır.</summary>
        [HttpGet]
        public async Task<ActionResult<List<HesapPlaniKaydiDto>>> Ara([FromQuery] string? q,
                                                                     [FromQuery] string? anaGrup,
                                                                     [FromQuery] int enFazla = 20,
                                                                     CancellationToken ct = default)
            => Ok(await _service.AraAsync(q, anaGrup, enFazla, ct));

        [HttpGet("sayi")]
        public async Task<ActionResult<int>> Sayi(CancellationToken ct)
            => Ok(await _service.SayAsync(ct));

        [HttpGet("kod/{kod}")]
        public async Task<ActionResult<HesapPlaniKaydiDto>> KodaGore(string kod, CancellationToken ct)
        {
            var dto = await _service.KodaGoreAsync(kod, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        /// <summary>xlsx içe aktarımı; beklenen kolonlar: <c>Hesap Kodu</c>, <c>Hesap Adı</c>.</summary>
        [HttpPost("ice-aktar")]
        [RequestSizeLimit(EnFazlaDosyaBayt)]
        public async Task<ActionResult<HesapPlaniIceAktarimSonucDto>> IceAktar(IFormFile file, CancellationToken ct = default)
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
                using var bellek = new MemoryStream();
                await using (var kaynak = file.OpenReadStream())
                    await kaynak.CopyToAsync(bellek, ct);
                bellek.Position = 0;

                return Ok(await _service.IceAktarAsync(bellek, ct));
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
    }
}
