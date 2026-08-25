using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.BankaEkstre.Controllers
{
    /// <summary>
    /// Ekstresi işlenen banka hesapları. Rota <c>api/catalog/*</c> altında olduğu için
    /// gateway'in mevcut <c>/catalog/{everything}</c> route'undan değişiklik olmadan geçer.
    /// </summary>
    [ApiController]
    [Route("api/catalog/banka-ekstre/banka-hesaplari")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class BankaHesaplariController : ControllerBase
    {
        private const long EnFazlaDosyaBayt = 20 * 1024 * 1024;
        private const string XlsxTuru = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IBankaHesabiService _service;
        private readonly IBankaHesabiIceAktarimService _iceAktarim;

        public BankaHesaplariController(IBankaHesabiService service, IBankaHesabiIceAktarimService iceAktarim)
        {
            _service = service;
            _iceAktarim = iceAktarim;
        }

        [HttpGet]
        public async Task<ActionResult<List<BankaHesabiDto>>> GetHepsi([FromQuery] bool pasifDahil = false,
                                                                      CancellationToken ct = default)
            => Ok(await _service.GetHepsiAsync(pasifDahil, ct));

        /// <summary>Hesap tanımlarken seçilebilecek ayrıştırıcılar.</summary>
        [HttpGet("parserler")]
        public ActionResult<List<ParserSecenekDto>> GetParserler()
            => Ok(_service.GetParserSecenekleri());

        /// <summary>
        /// Hesap adından eşleştirme anahtarı önerisi. Form yeni hesapta alanı bununla
        /// doldurur; kullanıcı düzenleyebilir, kaydeden değer formdaki değerdir.
        /// </summary>
        [HttpGet("anahtar-onerisi")]
        public ActionResult<AnahtarOnerisiDto> AnahtarOnerisi([FromQuery] string? hesapAdi,
                                                              [FromQuery] string? bankaAdi)
            => Ok(new AnahtarOnerisiDto { EslestirmeAnahtarlari = _service.AnahtarOner(hesapAdi, bankaAdi) });

        /// <summary>
        /// Firmanın hesap sahibi kimliği. Banka kapsülünün değil, Firma Tanımları ekranının
        /// alanı: değer hesap satırlarında dursa da firma bazlıdır.
        /// </summary>
        [HttpGet("hesap-sahibi")]
        public async Task<ActionResult<HesapSahibiKimlikDto>> HesapSahibi(CancellationToken ct)
            => Ok(await _service.HesapSahibiGetAsync(ct));

        /// <summary>Kimliği firmanın tüm hesaplarına yazar.</summary>
        [HttpPut("hesap-sahibi")]
        public async Task<ActionResult<HesapSahibiKimlikDto>> HesapSahibiKaydet([FromBody] HesapSahibiKimlikYazDto dto,
                                                                               CancellationToken ct)
            => Ok(await _service.HesapSahibiKaydetAsync(dto, ct));

        /// <summary>
        /// Hesap sahibinin henüz eklenmemiş yazımları. Bankalar aynı firmayı çok farklı
        /// yazıyor; "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş." gibi yazımlar ancak yüklenmiş
        /// ekstreler taranarak bulunur. Kullanıcı tek tıkla takma adlara ekler.
        /// </summary>
        [HttpGet("hesap-sahibi-onerileri")]
        public async Task<ActionResult<List<HesapSahibiOnerisiDto>>> HesapSahibiOnerileri(CancellationToken ct)
            => Ok(await _service.HesapSahibiOnerileriAsync(ct));

        /// <summary>
        /// Firmada kullanılan banka adları (+ hesap sayıları). Banka adı alanı serbest metin
        /// değil açılır liste; kaynağı burası.
        /// </summary>
        [HttpGet("banka-adlari")]
        public async Task<ActionResult<List<BankaAdiDto>>> BankaAdlari(CancellationToken ct)
            => Ok(await _service.BankaAdlariAsync(ct));

        /// <summary>
        /// Aynı bankanın farklı yazımlarını tek ada indirir. Kaç hesabın etkilendiği
        /// yanıtta döner; ekran onay adımında bu sayıyı önceden gösterir.
        /// </summary>
        [HttpPost("banka-adi-birlestir")]
        public async Task<ActionResult<BankaAdiBirlestirSonucDto>> BankaAdiBirlestir(
            [FromBody] BankaAdiBirlestirDto dto, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.BankaAdiBirlestirAsync(dto, ct));
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<BankaHesabiDto>> GetById(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BankaHesabiDto>> Create([FromBody] BankaHesabiYazDto dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.CreateAsync(dto, ct);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (DuplicateRecordException ex)
            {
                return Conflict(new { field = ex.Field, message = ex.Message });
            }
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<BankaHesabiDto>> Update(int id, [FromBody] BankaHesabiYazDto dto, CancellationToken ct)
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
            catch (BankaEkstreKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>
        /// Toplu içe aktarım (xlsx). Anahtar ORKA hesap kodu + firma: varsa güncellenir,
        /// yoksa eklenir. Dosyada olmayan hesaplara dokunulmaz. Doğrulama satır bazlıdır;
        /// hatalı satır atlanır, dosyanın kalanı işlenir.
        /// </summary>
        [HttpPost("ice-aktar")]
        [RequestSizeLimit(EnFazlaDosyaBayt)]
        public async Task<ActionResult<BankaHesabiIceAktarimSonucDto>> IceAktar(IFormFile file, CancellationToken ct = default)
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
            => File(_iceAktarim.SablonUret(), XlsxTuru, "banka-hesaplari-sablon.xlsx");

        /// <summary>Ekstresi olan hesap silinemez; kullanılmayacaksa pasife alınır.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var sonuc = await _service.DeleteAsync(id, ct);
            return sonuc switch
            {
                true => NoContent(),
                false => Conflict(new { message = "Bu hesaba ait ekstre yüklemesi var; hesap silinemez. Hesabı pasife alın." }),
                _ => NotFound()
            };
        }
    }
}
