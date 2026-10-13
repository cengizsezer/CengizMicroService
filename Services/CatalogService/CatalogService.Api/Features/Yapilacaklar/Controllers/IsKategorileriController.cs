using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Yapilacaklar.Controllers
{
    /// <summary>
    /// Yönetim → İş Kategorileri (Firma Panosu Aşama 2). Global; rota <c>api/catalog/*</c>, gateway değişmedi.
    /// Kullanılan kategori silinmez (400, "pasife alın").
    /// </summary>
    [ApiController]
    [Route("api/catalog/is-kategorileri")]
    [Authorize]
    public class IsKategorileriController : ControllerBase
    {
        private readonly IIsKategoriService _service;

        public IsKategorileriController(IIsKategoriService service) => _service = service;

        [HttpGet]
        public Task<ActionResult<List<IsKategorisiDto>>> Liste(CancellationToken ct = default)
            => Calistir(() => _service.ListeAsync(ct));

        [HttpPost]
        public Task<ActionResult<IsKategorisiDto>> Ekle([FromBody] IsKategorisiKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.EkleAsync(dto, ct));

        [HttpPut("{id:int}")]
        public Task<ActionResult<IsKategorisiDto>> Guncelle(int id, [FromBody] IsKategorisiKaydetDto dto, CancellationToken ct = default)
            => Calistir(() => _service.GuncelleAsync(id, dto, ct));

        /// <summary>Gövde: bütün kategori Id'leri yeni sırasıyla.</summary>
        [HttpPut("sira")]
        public Task<ActionResult<List<IsKategorisiDto>>> Sirala([FromBody] List<int> idler, CancellationToken ct = default)
            => Calistir(() => _service.SiralaAsync(idler ?? new(), ct));

        [HttpDelete("{id:int}")]
        public Task<ActionResult<object>> Sil(int id, CancellationToken ct = default)
            => Calistir<object>(async () => { await _service.SilAsync(id, ct); return new { silindi = id }; });

        /// <summary>Yasal varsayılanı bu kategoriye taşır (eskisinden kaldırılır).</summary>
        [HttpPost("{id:int}/yasal-varsayilan")]
        public Task<ActionResult<List<IsKategorisiDto>>> YasalVarsayilanYap(int id, CancellationToken ct = default)
            => Calistir(() => _service.YasalVarsayilanYapAsync(id, ct));

        [HttpGet("mukellefiyetler")]
        public Task<ActionResult<List<MukellefiyetEslemeDto>>> Mukellefiyetler(CancellationToken ct = default)
            => Calistir(() => _service.MukellefiyetlerAsync(ct));

        /// <summary>Gövdede <c>kategoriId</c> boş = eşlemeyi kaldır (yasal iş yasal varsayılana düşer).</summary>
        [HttpPut("mukellefiyetler/{kod}")]
        public Task<ActionResult<MukellefiyetEslemeDto>> MukellefiyetKaydet(string kod, [FromBody] MukellefiyetEslemeKaydetDto dto,
                                                                            CancellationToken ct = default)
            => Calistir(() => _service.MukellefiyetKaydetAsync(kod, dto, ct));

        [HttpGet("kategorisiz-isler")]
        public Task<ActionResult<List<KategorisizGrupDto>>> Kategorisiz(CancellationToken ct = default)
            => Calistir(() => _service.KategorisizAsync(ct));

        [HttpPost("kategorisiz-isler/ata")]
        public Task<ActionResult<TopluKategoriSonucDto>> TopluAta([FromBody] TopluKategoriAtaDto dto, CancellationToken ct = default)
            => Calistir(() => _service.TopluAtaAsync(dto, ct));

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
