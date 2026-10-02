using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Yapilacaklar.Controllers
{
    /// <summary>
    /// Takip panosunun "Nasıl yapılır" sheet'i (Prompt 14): tarif İŞ BAZINDA. Global — firma üstü.
    /// Rota <c>api/catalog/*</c>, gateway değişmedi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/is-tarifleri")]
    [Authorize]
    public class IsTarifleriController : ControllerBase
    {
        private readonly IIsTarifiService _service;

        public IsTarifleriController(IIsTarifiService service) => _service = service;

        [HttpGet]
        public Task<ActionResult<List<IsTarifiOzetDto>>> Liste(CancellationToken ct = default)
            => Calistir(() => _service.ListeAsync(ct));

        /// <summary><c>anahtar</c>: "y-0015-2" ya da "o-bordro".</summary>
        [HttpGet("detay")]
        public Task<ActionResult<IsTarifiDetayDto>> Detay([FromQuery] string anahtar, CancellationToken ct = default)
            => Calistir(() => _service.DetayAsync(anahtar, ct));

        /// <summary>
        /// Tarif oluştur. Gövde BOŞSA grubun en dolu kaydından (Prompt 14); gövde DOLUYSA formdaki metinden
        /// (Prompt 15 "+ Tarif yaz" — aynı uç, girdisi genişledi). Metni boş form reddedilir. Farklı metinli
        /// kayıt bağlanmaz, raporda döner. <c>firmaId</c>: yasal işte prosedür satırı yoksa açılacak firma.
        /// </summary>
        [HttpPost]
        public Task<ActionResult<TarifUretimRaporuDto>> Olustur([FromQuery] string anahtar, [FromQuery] int? firmaId,
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] IsTarifiKaydetDto? form,
            CancellationToken ct = default)
            => Calistir(() => _service.UretAsync(anahtar, ct, form, firmaId));

        [HttpPut("{id:int}")]
        public Task<ActionResult<IsTarifiDto>> Guncelle(int id, [FromBody] IsTarifiKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.GuncelleAsync(id, dto, ct));

        /// <summary>"Bu tarifi kullan" — kaydın kendi metni silinmez.</summary>
        [HttpPost("{id:int}/bagla/{firmaIsiId:int}")]
        public async Task<IActionResult> Bagla(int id, int firmaIsiId, CancellationToken ct = default)
        {
            try
            {
                await _service.BaglaAsync(id, firmaIsiId, ct);
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

        /// <summary>"N kaydın hepsini bağla" — yalnız metni boş/aynı kayıtlar; farklı metin atlanır.</summary>
        [HttpPost("{id:int}/bagla-hepsi")]
        public Task<ActionResult<object>> HepsiniBagla(int id, [FromBody] List<int> firmaIsiIdleri, CancellationToken ct = default)
            => Calistir<object>(async () => new { baglanan = await _service.HepsiniBaglaAsync(id, firmaIsiIdleri ?? new(), ct) });

        /// <summary>Tarif eki. Dosya önce FileApiService'e yüklenir (prosedür/dönem ekiyle aynı akış ve sınır).</summary>
        [HttpPost("{id:int}/ekler")]
        public Task<ActionResult<FirmaIsiEkiDto>> EkEkle(int id, [FromBody] FirmaIsiEkiOlusturDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkEkleAsync(id, dto, ct));

        /// <summary>Yanıttaki <c>fileId</c> FileApiService'ten de silinmelidir.</summary>
        [HttpDelete("{id:int}/ekler/{ekId:int}")]
        public Task<ActionResult<object>> EkSil(int id, int ekId, CancellationToken ct = default)
            => Calistir<object>(async () => new { fileId = await _service.EkSilAsync(id, ekId, ct) });

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
                return NotFound(new { field = "id", message = ex.Message });
            }
        }
    }
}
