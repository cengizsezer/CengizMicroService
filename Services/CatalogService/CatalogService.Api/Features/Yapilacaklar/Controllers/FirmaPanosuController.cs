using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Yapilacaklar.Controllers
{
    /// <summary>
    /// Firma Panosu → Yapılacaklar (Aşama 1): firma + dönem checklist kartları, sayaçlar ve liste tek uçta.
    /// Kartlar yalnız okur (Aşama 3'te şablondan iş yazma uçları eklendi); işaretleme Yapılacaklar'ın mevcut <c>PUT yapilacaklar/isaretle</c> ucudur (yeni yazma yolu yok).
    /// Rota <c>api/catalog/*</c> altında, gateway değişmedi.
    /// </summary>
    [ApiController]
    [Route("api/catalog/firma-panosu")]
    [Authorize]
    public class FirmaPanosuController : ControllerBase
    {
        private readonly IYapilacaklarService _service;
        private readonly IChecklistSablonService _sablonlar;

        public FirmaPanosuController(IYapilacaklarService service, IChecklistSablonService sablonlar)
        {
            _service = service;
            _sablonlar = sablonlar;
        }

        /// <param name="firma">Tek firma; boş = bütün aktif firmalar.</param>
        /// <param name="tur">Virgülle: <c>yasal</c>, <c>ozel</c>. Boş = hepsi.</param>
        /// <param name="sorumlu">Virgülle kullanıcı Id'leri; <c>0</c> = sorumlusu olmayan. Boş = hepsi.</param>
        [HttpGet("kartlar")]
        public async Task<ActionResult<FirmaPanosuDto>> Kartlar([FromQuery] int? firma = null, [FromQuery] string? tur = null,
                                                                [FromQuery] string? sorumlu = null, CancellationToken ct = default)
        {
            var filtre = new FirmaPanosuFiltresi { FirmaId = firma };

            foreach (var t in Parcalar(tur))
            {
                switch (t.ToLowerInvariant())
                {
                    case "yasal": filtre.Turler.Add(IsKaynagi.Yasal); break;
                    case "ozel": filtre.Turler.Add(IsKaynagi.Ozel); break;
                    default: return BadRequest(new { field = "tur", message = $"Geçersiz tür: {t}" });
                }
            }

            foreach (var s in Parcalar(sorumlu))
            {
                if (!int.TryParse(s, out var id) || id < 0)
                    return BadRequest(new { field = "sorumlu", message = $"Geçersiz sorumlu: {s}" });
                filtre.Sorumlular.Add(id);
            }

            try
            {
                return Ok(await _service.FirmaPanosuAsync(filtre, ct));
            }
            catch (YapilacaklarKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>
        /// Aşama 3: şablondan iş oluşturmanın ÖNİZLEMESİ — yazmaz; firma başına eklenecek ve atlanacak maddeler.
        /// Gerçek yazmayla aynı plan (<c>ChecklistSablonPlanlayici.Planla</c>).
        /// </summary>
        /// <param name="firma">Birden çok: <c>?firma=3&amp;firma=5</c>.</param>
        [HttpGet("isler/sablondan/onizle")]
        public Task<ActionResult<SablondanSonucDto>> SablondanOnizle([FromQuery] int sablon, [FromQuery] List<int> firma,
                                                                    [FromQuery] string? ilkDonem = null, CancellationToken ct = default)
            => Calistir(() => _sablonlar.SablondanOnizleAsync(new SablondanIstekDto { SablonId = sablon, FirmaIdleri = firma, IlkDonem = ilkDonem }, ct));

        /// <summary>Şablonun maddelerini seçilen firmalara özel iş olarak yazar (tek transaction). Aynı başlık + tekrar atlanır.</summary>
        [HttpPost("isler/sablondan")]
        public Task<ActionResult<SablondanSonucDto>> Sablondan([FromBody] SablondanIstekDto istek, CancellationToken ct = default)
            => Calistir(() => _sablonlar.SablondanOlusturAsync(istek, ct));

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
        }

        private static IEnumerable<string> Parcalar(string? metin)
            => (metin ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
