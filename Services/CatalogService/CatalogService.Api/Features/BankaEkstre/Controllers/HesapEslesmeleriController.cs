using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Öğrenilen eşleşmeler. Yanlış onaylanan bir eşleşme bir daha sorulmadan tekrarlanır;
    /// bu yüzden liste görülebilir, düzeltilebilir ve silinebilir olmak zorunda.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/eslesmeler")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class HesapEslesmeleriController : ControllerBase
    {
        private const long EnFazlaDosyaBayt = 20 * 1024 * 1024;
        private const string XlsxTuru = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IHesapEslesmeService _service;
        private readonly IOgrenilenEslesmeIceAktarimService _iceAktarim;

        public HesapEslesmeleriController(IHesapEslesmeService service,
                                          IOgrenilenEslesmeIceAktarimService iceAktarim)
        {
            _service = service;
            _iceAktarim = iceAktarim;
        }

        [HttpGet]
        public async Task<ActionResult<List<HesapEslesmesiDto>>> Ara([FromQuery] string? q,
                                                                    [FromQuery] int enFazla = 100,
                                                                    CancellationToken ct = default)
            => Ok(await _service.AraAsync(q, enFazla, ct));

        [HttpPut("{id:int}")]
        public async Task<ActionResult<HesapEslesmesiDto>> Guncelle(int id, [FromBody] HesapEslesmesiYazDto dto,
                                                                   CancellationToken ct = default)
        {
            try
            {
                var kayit = await _service.GuncelleAsync(id, dto, ct);
                return kayit is null ? NotFound() : Ok(kayit);
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>
        /// Toplu içe aktarım (xlsx). ORKA yevmiyesinden çıkarılmış doğrulanmış eşleşmeler
        /// için: onay ekranından tek tek geçmekle aynı şey, sadece toplu hali.
        ///
        /// Mevcut kayıt <b>üzerine yazılmaz</b> — kullanıcının onay ekranında verdiği karar,
        /// geçmişten türetilen kayda göre önceliklidir. Doğrulama satır bazlıdır; hatalı
        /// satır atlanır, dosyanın kalanı işlenir.
        /// </summary>
        [HttpPost("ice-aktar")]
        [RequestSizeLimit(EnFazlaDosyaBayt)]
        public async Task<ActionResult<OgrenilenEslesmeIceAktarimSonucDto>> IceAktar(IFormFile file,
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
                using var bellek = new MemoryStream();
                await using (var kaynak = file.OpenReadStream())
                    await kaynak.CopyToAsync(bellek, ct);
                bellek.Position = 0;

                return Ok(await _iceAktarim.IceAktarAsync(bellek, ct));
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { field = "file", message = ex.Message });
            }
            catch (Exception ex) when (ex.GetType().FullName?.Contains("ClosedXML", StringComparison.OrdinalIgnoreCase) == true)
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                    new { field = "file", message = $"Excel dosyası okunamadı: {ex.Message}" });
            }
        }

        /// <summary>Doğru başlıklara sahip boş şablon; kullanıcı kolon adlarını tahmin etmesin.</summary>
        [HttpGet("sablon")]
        public IActionResult Sablon()
            => File(_iceAktarim.SablonUret(), XlsxTuru, "ogrenilen-eslesmeler-sablon.xlsx");

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Sil(int id, CancellationToken ct)
            => await _service.SilAsync(id, ct) ? NoContent() : NotFound();
    }
}
