using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Yapilacaklar.Controllers
{
    /// <summary>
    /// Yapılacaklar ekranı ve anasayfa şeridi. Firma üstü: seçili firmadan bağımsız, bütün
    /// aktif firmaların işleri. Rota <c>api/catalog/*</c> altında, gateway değişmedi.
    ///
    /// İşaretleme birden çok firmayı birlikte yazabildiği için (aynı iş tek satır) firma
    /// kapsam filtresi yok; her firma-dönem servis tarafında tek tek doğrulanır.
    /// </summary>
    [ApiController]
    [Route("api/catalog/yapilacaklar")]
    [Authorize]
    public class YapilacaklarController : ControllerBase
    {
        private readonly IYapilacaklarService _service;

        public YapilacaklarController(IYapilacaklarService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<YapilacaklarDto>> Liste([FromQuery] IsFiltresi filtre = IsFiltresi.Tumu, [FromQuery] int? firmaId = null,
                                                               CancellationToken ct = default)
        {
            try
            {
                return Ok(await _service.ListeAsync(filtre, ct, firmaId));
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Anasayfa şeridi: gecikmiş ve bu hafta satır sayısı (bütün firmalar).</summary>
        [HttpGet("ozet")]
        public async Task<ActionResult<YapilacaklarOzetDto>> Ozet(CancellationToken ct = default)
            => Ok(await _service.OzetAsync(ct));

        /// <summary>
        /// Tek ya da çok firma-dönem; <c>Yapildi = false</c> işareti kaldırır. Notsuz/kanıtsız satır
        /// silinir; notu ya da kanıtı olan dönemde yalnız zaman boşalır (not ve kanıt korunur).
        /// </summary>
        [HttpPut("isaretle")]
        public async Task<IActionResult> Isaretle([FromBody] IsIsaretleDto dto, CancellationToken ct = default)
        {
            try
            {
                // fileIds: işareti kaldırılan dönemlerin kanıt dosyaları — istemci FileApi'den de siler.
                var fileIds = await _service.IsaretleAsync(dto, ct);
                return Ok(new { fileIds });
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }
    }

    /// <summary>
    /// Anasayfa "Firma işleri" kartı: seçili firmanın yasal ve özel işleri, özel iş tanımı.
    /// Firma rotadaki kayıttan gelir; kapsamı <see cref="BankaFirmaFiltresi"/> doğrular.
    /// </summary>
    [ApiController]
    [Route("api/catalog/anasayfa/firmalar/{firmaId:int}/isler")]
    [Authorize]
    [ServiceFilter(typeof(BankaFirmaFiltresi))]
    public class FirmaIsleriController : ControllerBase
    {
        private readonly IYapilacaklarService _service;

        public FirmaIsleriController(IYapilacaklarService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<FirmaIsleriKartDto>> Kart(int firmaId, CancellationToken ct = default)
            => Ok(await _service.FirmaKartiAsync(firmaId, ct));

        [HttpPost]
        public Task<ActionResult<FirmaIsiDto>> Ekle(int firmaId, [FromBody] FirmaIsiKaydetDto dto,
                                                    CancellationToken ct = default)
            => Calistir(() => _service.IsEkleAsync(firmaId, dto, ct));

        [HttpPut("{isId:int}")]
        public Task<ActionResult<FirmaIsiDto>> Guncelle(int firmaId, int isId, [FromBody] FirmaIsiKaydetDto dto,
                                                        CancellationToken ct = default)
            => Calistir(() => _service.IsGuncelleAsync(firmaId, isId, dto, ct));

        /// <summary>Yanıttaki <c>fileIds</c> (işin ekleri) FileApiService'ten de silinmelidir.</summary>
        [HttpDelete("{isId:int}")]
        public async Task<ActionResult<object>> Sil(int firmaId, int isId, CancellationToken ct = default)
        {
            try
            {
                var fileIds = await _service.IsSilAsync(firmaId, isId, ct);
                return Ok(new { fileIds });
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "isId", message = ex.Message });
            }
        }

        // ---- Toplu ekleme ve kopyalama (Prompt 11) ----

        /// <summary>Yapıştırılan tabloyu çözer; EKLEMEZ. Düzeltmeler aynı uca hücre tablosuyla geri gelir.</summary>
        [HttpPost("toplu/onizle")]
        public Task<ActionResult<TopluOnizlemeDto>> TopluOnizle(int firmaId, [FromBody] TopluOnizleIstekDto istek,
                                                               CancellationToken ct = default)
            => Calistir(() => _service.TopluOnizleAsync(firmaId, istek, ct));

        [HttpPost("toplu/ekle")]
        public Task<ActionResult<TopluSonucDto>> TopluEkle(int firmaId, [FromBody] TopluEkleDto dto, CancellationToken ct = default)
            => Calistir(() => _service.TopluEkleAsync(firmaId, dto, ct));

        /// <summary>Başka firmanın özel işlerini bu firmaya kopyalar.</summary>
        [HttpPost("kopyala")]
        public Task<ActionResult<TopluSonucDto>> Kopyala(int firmaId, [FromBody] IsKopyalaDto dto, CancellationToken ct = default)
            => Calistir(() => _service.KopyalaAsync(firmaId, dto, ct));

        /// <summary>Boş şablon (xlsx): doğru başlık satırı + örnek satır. Firma verisi okunmaz.</summary>
        [HttpGet("sablon")]
        public IActionResult Sablon(int firmaId)
            => File(YapilacaklarService.IsSablonu(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "firma-isleri-sablonu.xlsx");

        // ---- Prosedür (Prompt 8) ----

        /// <summary>
        /// Prosedür paneli. <paramref name="kaynakId"/> özelde iş Id'si, yasalda serinin herhangi
        /// bir takvim satırı (listedeki satırın <c>KaynakId</c>'si).
        /// </summary>
        [HttpGet("prosedur")]
        public Task<ActionResult<IsProsedurDto>> Prosedur(int firmaId, [FromQuery] IsKaynagi kaynakTip, [FromQuery] int kaynakId,
                                                         [FromQuery] bool tumGecmis = false, CancellationToken ct = default)
            => Calistir(() => _service.ProsedurAsync(firmaId, kaynakTip, kaynakId, tumGecmis, ct));

        /// <summary>Yasal işin prosedür satırını açar (idempotent) — ek yüklemeden önce.</summary>
        [HttpPost("yasal/{kod}/{tekrar}")]
        public Task<ActionResult<FirmaIsiDto>> YasalAc(int firmaId, string kod, IsTekrari tekrar, CancellationToken ct = default)
            => Calistir(() => _service.YasalProsedurAcAsync(firmaId, kod, tekrar, ct));

        /// <summary>Yasal işin program, alıcı, menü yolu ve adımları; ad ve tarih değişmez.</summary>
        [HttpPut("yasal/{kod}/{tekrar}/prosedur")]
        public Task<ActionResult<FirmaIsiDto>> YasalProsedur(int firmaId, string kod, IsTekrari tekrar,
                                                             [FromBody] IsProsedurKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.YasalProsedurKaydetAsync(firmaId, kod, tekrar, dto, ct));

        /// <summary>Ek kaydı. Dosya <b>önce</b> FileApiService'e yüklenir (Belgeler kartıyla aynı akış).</summary>
        [HttpPost("{isId:int}/ekler")]
        public Task<ActionResult<FirmaIsiEkiDto>> EkEkle(int firmaId, int isId, [FromBody] FirmaIsiEkiOlusturDto dto,
                                                         CancellationToken ct = default)
            => Calistir(() => _service.EkEkleAsync(firmaId, isId, dto, ct));

        /// <summary>Yanıttaki <c>fileId</c> FileApiService'ten de silinmelidir.</summary>
        [HttpDelete("{isId:int}/ekler/{ekId:int}")]
        public async Task<ActionResult<object>> EkSil(int firmaId, int isId, int ekId, CancellationToken ct = default)
        {
            try
            {
                var fileId = await _service.EkSilAsync(firmaId, isId, ekId, ct);
                return Ok(new { fileId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "ekId", message = ex.Message });
            }
        }

        private async Task<ActionResult<T>> Calistir<T>(Func<Task<T>> is_)
        {
            try
            {
                return Ok(await is_());
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "isId", message = ex.Message });
            }
        }
    }

    /// <summary>Yönetim → Vergi Takvimi. Global tablo; firma kapsamı yok.</summary>
    [ApiController]
    [Route("api/catalog/vergi-takvimi")]
    [Authorize]
    public class VergiTakvimiController : ControllerBase
    {
        private readonly IVergiTakvimiService _service;

        public VergiTakvimiController(IVergiTakvimiService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<VergiTakvimiDto>>> Liste([FromQuery] int? yil, CancellationToken ct = default)
            => Ok(await _service.ListeAsync(yil, ct));

        [HttpPost]
        public Task<ActionResult<VergiTakvimiDto>> Ekle([FromBody] VergiTakvimiKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkleAsync(dto, ct));

        [HttpPut("{id:int}")]
        public Task<ActionResult<VergiTakvimiDto>> Guncelle(int id, [FromBody] VergiTakvimiKaydetDto dto,
                                                            CancellationToken ct = default)
            => Calistir(() => _service.GuncelleAsync(id, dto, ct));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Sil(int id, CancellationToken ct = default)
        {
            try
            {
                await _service.SilAsync(id, ct);
                return NoContent();
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "id", message = ex.Message });
            }
        }

        /// <summary>Eksik varsayılan satırları ekler; mevcut satıra dokunmaz.</summary>
        [HttpPost("varsayilanlar")]
        public async Task<ActionResult<object>> Varsayilanlar(CancellationToken ct = default)
            => Ok(new { eklenen = await _service.VarsayilanlariYukleAsync(ct) });

        private async Task<ActionResult<VergiTakvimiDto>> Calistir(Func<Task<VergiTakvimiDto>> is_)
        {
            try
            {
                return Ok(await is_());
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "id", message = ex.Message });
            }
        }
    }
}
