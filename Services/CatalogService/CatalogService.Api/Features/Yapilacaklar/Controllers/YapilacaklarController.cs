using CatalogService.Api.Features.BankaEkstre.Kapsam;
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
        public async Task<ActionResult<YapilacaklarDto>> Liste([FromQuery] IsFiltresi filtre = IsFiltresi.Tumu,
                                                               CancellationToken ct = default)
        {
            try
            {
                return Ok(await _service.ListeAsync(filtre, ct));
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

        /// <summary>Tek ya da çok firma-dönem; <c>Yapildi = false</c> işareti kaldırır (satır silinir).</summary>
        [HttpPut("isaretle")]
        public async Task<IActionResult> Isaretle([FromBody] IsIsaretleDto dto, CancellationToken ct = default)
        {
            try
            {
                await _service.IsaretleAsync(dto, ct);
                return NoContent();
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

        [HttpDelete("{isId:int}")]
        public async Task<IActionResult> Sil(int firmaId, int isId, CancellationToken ct = default)
        {
            try
            {
                await _service.IsSilAsync(firmaId, isId, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { field = "isId", message = ex.Message });
            }
        }

        private async Task<ActionResult<FirmaIsiDto>> Calistir(Func<Task<FirmaIsiDto>> is_)
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
