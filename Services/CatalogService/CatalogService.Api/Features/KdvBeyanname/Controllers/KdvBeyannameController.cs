using System.Net.Http.Json;
using CatalogService.Api.Features.KdvBeyanname.Dtos;
using CatalogService.Api.Features.KdvBeyanname.Services;
using CatalogService.Api.Features.KdvBeyanname.Services.BdpXml;
using CatalogService.Api.Features.KdvBeyanname.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.KdvBeyanname.Controllers
{
    [ApiController]
    [Route("api/catalog/kdv-beyanname")]
    public class KdvBeyannameController : ControllerBase
    {
        private readonly IKdvBeyannameQueryService _query;
        private readonly IDuzenleyenService _duzenleyen;
        private readonly IKdvUploadService _upload;
        private readonly IKarsilastirmaService _karsilastirma;
        private readonly IKdvSonucService _sonuc;
        private readonly IBdpXmlBuilder _xmlBuilder;
        private readonly CatalogContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<KdvBeyannameController> _logger;

        public KdvBeyannameController(
            IKdvBeyannameQueryService query,
            IDuzenleyenService duzenleyen,
            IKdvUploadService upload,
            IKarsilastirmaService karsilastirma,
            IKdvSonucService sonuc,
            IBdpXmlBuilder xmlBuilder,
            CatalogContext db,
            IHttpClientFactory httpFactory,
            IConfiguration config,
            ILogger<KdvBeyannameController> logger)
        {
            _query = query;
            _duzenleyen = duzenleyen;
            _upload = upload;
            _karsilastirma = karsilastirma;
            _sonuc = sonuc;
            _xmlBuilder = xmlBuilder;
            _db = db;
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        // ── Firma kart listesi ─────────────────────────────────────────────

        [HttpGet("firmalar")]
        [ProducesResponseType(typeof(List<KdvFirmaCardDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<KdvFirmaCardDto>>> GetFirmalar(CancellationToken ct)
        {
            var list = await _query.GetFirmaCardsAsync(ct);
            return Ok(list);
        }

        // ── Tarama tetikleme (Sovos.IncomingInvoiceWorker'a proxy) ─────────

        [HttpPost("{firmaId:int}/tara")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> TaraTetikle(
            int firmaId,
            [FromBody] TaramaTetikleRequest req,
            CancellationToken ct)
        {
            if (req is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });
            if (req.BaslangicTarihi > req.BitisTarihi)
                return BadRequest(new { mesaj = "Başlangıç tarihi bitiş tarihinden büyük olamaz." });

            var workerBaseUrl = _config["IncomingInvoiceWorker:BaseUrl"];
            if (string.IsNullOrWhiteSpace(workerBaseUrl))
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    mesaj = "IncomingInvoiceWorker:BaseUrl yapılandırılmadı. appsettings'e ekleyin."
                });

            var client = _httpFactory.CreateClient("incoming-invoice-worker");
            client.BaseAddress = new Uri(workerBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);

            try
            {
                var resp = await client.PostAsJsonAsync(
                    $"api/kdv-beyanname/{firmaId}/tara",
                    new
                    {
                        baslangicTarihi = req.BaslangicTarihi,
                        bitisTarihi = req.BitisTarihi
                    },
                    ct);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return NotFound(new { firmaId, mesaj = "Firma bulunamadı." });

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "Worker tarama tetikleme başarısız: {Status} {Body}",
                        resp.StatusCode, body);
                    return StatusCode((int)resp.StatusCode, new
                    {
                        mesaj = "Worker tarama isteğini reddetti.",
                        detay = body
                    });
                }

                return Accepted(new { firmaId, status = "queued" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Worker'a bağlanılamadı (FirmaId={Id}, BaseUrl={Url})",
                    firmaId, workerBaseUrl);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    mesaj = "Worker servisine ulaşılamadı.",
                    detay = ex.Message
                });
            }
        }

        // ── Tarama geçmişi ─────────────────────────────────────────────────

        [HttpGet("{firmaId:int}/taramalar")]
        [ProducesResponseType(typeof(List<TaramaDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<TaramaDto>>> GetTaramalar(
            int firmaId,
            [FromQuery] int take = 20,
            CancellationToken ct = default)
        {
            var list = await _query.GetTaramalarAsync(firmaId, take, ct);
            return Ok(list);
        }

        // ── Tab 1: Gelen Faturalar ─────────────────────────────────────────

        [HttpGet("{firmaId:int}/gelen-faturalar")]
        [ProducesResponseType(typeof(List<GelenFaturaDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<GelenFaturaDto>>> GetGelenFaturalar(
            int firmaId,
            [FromQuery] DateTime? baslangic = null,
            [FromQuery] DateTime? bitis = null,
            CancellationToken ct = default)
        {
            var list = await _query.GetGelenFaturalarAsync(firmaId, baslangic, bitis, ct);
            return Ok(list);
        }

        // ── Tab 2: Yevmiye upload & listele ────────────────────────────────

        [HttpPost("{firmaId:int}/yevmiye/upload")]
        [RequestSizeLimit(50_000_000)] // 50 MB
        [ProducesResponseType(typeof(YevmiyeUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<YevmiyeUploadResultDto>> UploadYevmiye(
            int firmaId,
            [FromQuery] string donem,
            IFormFile file,
            CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { mesaj = "Excel dosyası yüklenmedi." });

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _upload.UploadYevmiyeAsync(firmaId, donem, stream, ct);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
            catch (KdvYevmiyeBaslikException ex)
            {
                // Başlık eşleşmedi → kullanıcıya "bulunan vs beklenen" göster.
                _logger.LogWarning(ex,
                    "Yevmiye başlık eşleşmedi (FirmaId={Id}, Donem={D})", firmaId, donem);
                return BadRequest(new YevmiyeBaslikHataDto
                {
                    Mesaj = ex.Message,
                    BulunanBasliklar = ex.BulunanBasliklar.ToList(),
                    BeklenenKolonlar = BuildBeklenenKolonlar(ex.BulunanBasliklar)
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Yevmiye parse hatası (FirmaId={Id}, Donem={D})", firmaId, donem);
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Yevmiye beklenen sütun başlıkları (tek kaynaktan: KdvYevmiyeColumns) ──

        [HttpGet("yevmiye/beklenen-basliklar")]
        [ProducesResponseType(typeof(List<BeklenenKolonDto>), StatusCodes.Status200OK)]
        public ActionResult<List<BeklenenKolonDto>> GetYevmiyeBeklenenBasliklar()
            => Ok(BuildBeklenenKolonlar(Array.Empty<string>()));

        // Tek kaynaktan BeklenenKolonDto listesi üretir. bulunanBasliklar verilirse
        // her kolonun "Bulundu" bayrağını parser ile aynı kuralla (case-insensitive,
        // trim'li) işaretler.
        private static List<BeklenenKolonDto> BuildBeklenenKolonlar(
            IReadOnlyCollection<string> bulunanBasliklar)
            => KdvYevmiyeColumns.All.Select(c => new BeklenenKolonDto
            {
                Ad            = c.Ad,
                Alternatifler = c.Alternatifler.ToList(),
                Zorunlu       = c.Zorunlu,
                Bulundu       = bulunanBasliklar.Count > 0
                                && KdvYevmiyeColumns.Eslesti(c, bulunanBasliklar)
            }).ToList();

        [HttpGet("{firmaId:int}/yevmiye")]
        [ProducesResponseType(typeof(List<YevmiyeSatirDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<YevmiyeSatirDto>>> GetYevmiye(
            int firmaId,
            [FromQuery] string donem,
            CancellationToken ct)
        {
            return Ok(await _query.GetYevmiyeAsync(firmaId, donem, ct));
        }

        // ── Tab 3: Mizan upload & listele ──────────────────────────────────

        [HttpPost("{firmaId:int}/mizan/upload")]
        [RequestSizeLimit(50_000_000)] // 50 MB
        [ProducesResponseType(typeof(MizanUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MizanUploadResultDto>> UploadMizan(
            int firmaId,
            [FromQuery] string donem,
            IFormFile file,
            CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { mesaj = "Excel dosyası yüklenmedi." });

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _upload.UploadMizanAsync(firmaId, donem, stream, ct);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Mizan parse hatası (FirmaId={Id}, Donem={D})", firmaId, donem);
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        [HttpGet("{firmaId:int}/mizan")]
        [ProducesResponseType(typeof(List<MizanSatirDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<MizanSatirDto>>> GetMizan(
            int firmaId,
            [FromQuery] string donem,
            CancellationToken ct)
        {
            return Ok(await _query.GetMizanAsync(firmaId, donem, ct));
        }

        // ── Tab 2: Karşılaştırma (Gelen Faturalar ↔ Yevmiye) ───────────────

        [HttpGet("{firmaId:int}/karsilastirma")]
        [ProducesResponseType(typeof(KarsilastirmaSonucuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<KarsilastirmaSonucuDto>> GetKarsilastirma(
            int firmaId,
            [FromQuery] string donem,
            CancellationToken ct)
        {
            try
            {
                var result = await _karsilastirma.KarsilastirAsync(firmaId, donem, ct);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Tab 4: Sonuç (KDV hesaplaması + BDP eksiklik kontrolü) ─────────

        [HttpGet("{firmaId:int}/sonuc")]
        [ProducesResponseType(typeof(KdvSonucDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<KdvSonucDto>> GetSonuc(
            int firmaId,
            [FromQuery] string donem,
            CancellationToken ct)
        {
            try
            {
                var result = await _sonuc.HesaplaAsync(firmaId, donem, ct);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Tab 4: BDP XML indir (KDV1_44, ISO-8859-9) ─────────────────────

        [HttpGet("{firmaId:int}/xml")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> XmlIndir(
            int firmaId,
            [FromQuery] string donem,
            CancellationToken ct)
        {
            // 1) Sonucu hesapla — eksiklik / hata varsa erken çık.
            KdvSonucDto sonuc;
            try
            {
                sonuc = await _sonuc.HesaplaAsync(firmaId, donem, ct);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return BadRequest(new { mesaj = ex.Message });
            }

            if (sonuc.Eksiklikler.VarMi)
            {
                return BadRequest(new
                {
                    mesaj = "Firma bilgilerinde eksik var, Firmalarım sayfasına gidin.",
                    eksiklikler = sonuc.Eksiklikler
                });
            }

            // 2) Firma + Düzenleyen kayıtlarını yükle.
            var firma = await _db.Firmalar.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == firmaId, ct);
            if (firma is null)
                return NotFound(new { mesaj = $"Firma bulunamadı: Id={firmaId}" });

            if (firma.DuzenleyenId is null)
                return BadRequest(new
                {
                    mesaj = "Bu firma için düzenleyen seçilmemiş. Firmalarım sayfasından seçin."
                });

            var duzenleyen = await _db.Duzenleyenler.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == firma.DuzenleyenId.Value, ct);
            if (duzenleyen is null || !duzenleyen.Aktif)
                return BadRequest(new
                {
                    mesaj = "Seçili düzenleyen bulunamadı veya pasif. " +
                            "Firmalarım sayfasından yeni düzenleyen seçin."
                });

            // 3) BDP XML üret (ISO-8859-9).
            var output = _xmlBuilder.Build(firma, duzenleyen, sonuc);
            return File(output.Content, output.ContentType, output.FileName);
        }

        // ── Düzenleyen CRUD (yönetim sayfası) ──────────────────────────────

        [HttpGet("duzenleyenler")]
        [ProducesResponseType(typeof(List<DuzenleyenDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<DuzenleyenDto>>> ListDuzenleyenler(
            [FromQuery] bool includeInactive = false,
            CancellationToken ct = default)
            => Ok(await _duzenleyen.ListAsync(includeInactive, ct));

        [HttpGet("duzenleyenler/{id:int}")]
        [ProducesResponseType(typeof(DuzenleyenDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DuzenleyenDto>> GetDuzenleyen(int id, CancellationToken ct)
        {
            var d = await _duzenleyen.GetAsync(id, ct);
            return d is null ? NotFound(new { mesaj = $"Düzenleyen bulunamadı: Id={id}" }) : Ok(d);
        }

        [HttpPost("duzenleyenler")]
        [ProducesResponseType(typeof(DuzenleyenDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DuzenleyenDto>> CreateDuzenleyen(
            [FromBody] DuzenleyenCreateDto dto,
            CancellationToken ct)
        {
            if (dto is null) return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });
            var created = await _duzenleyen.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetDuzenleyen), new { id = created.Id }, created);
        }

        [HttpPut("duzenleyenler/{id:int}")]
        [ProducesResponseType(typeof(DuzenleyenDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DuzenleyenDto>> UpdateDuzenleyen(
            int id,
            [FromBody] DuzenleyenUpdateDto dto,
            CancellationToken ct)
        {
            if (dto is null) return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });
            var updated = await _duzenleyen.UpdateAsync(id, dto, ct);
            return updated is null
                ? NotFound(new { mesaj = $"Düzenleyen bulunamadı: Id={id}" })
                : Ok(updated);
        }

        [HttpDelete("duzenleyenler/{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDuzenleyen(int id, CancellationToken ct)
        {
            var ok = await _duzenleyen.SoftDeleteAsync(id, ct);
            return ok ? NoContent() : NotFound(new { mesaj = $"Düzenleyen bulunamadı: Id={id}" });
        }
    }
}
