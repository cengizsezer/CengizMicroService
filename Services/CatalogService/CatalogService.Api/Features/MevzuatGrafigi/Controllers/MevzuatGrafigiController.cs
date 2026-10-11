using CatalogService.Api.Features.MevzuatGrafigi.Dtos;
using CatalogService.Api.Features.MevzuatGrafigi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.MevzuatGrafigi.Controllers
{
    /// <summary>
    /// Mevzuat grafiği (kartlar, bağlantılar, dışlamalar, tuvaller, şart takipleri).
    /// Yetki Mevzuat Notları ile aynı: giriş yapmış herkes okur ve yazar. Yazma uçları
    /// bilinçli olarak admin/ önekinde değil.
    /// Kural ihlali → 400, gövdede adı geçen kayıt yok → 404; ikisi de { message } döner.
    /// </summary>
    [ApiController]
    [Route("api/catalog/mevzuat-grafigi")]
    [Authorize]
    public class MevzuatGrafigiController : ControllerBase
    {
        private readonly IMevzuatGrafigiService _service;

        public MevzuatGrafigiController(IMevzuatGrafigiService service) => _service = service;

        [HttpGet]
        [ProducesResponseType(typeof(MevzuatGrafigiDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MevzuatGrafigiDto>> Get(CancellationToken ct)
            => Ok(await _service.GetAsync(ct));

        // ---- Kart ----

        [HttpPost("kartlar")]
        public Task<IActionResult> CreateKart([FromBody] MevzuatKartSaveDto dto, CancellationToken ct)
            => Calistir(async () => Ok(await _service.CreateKartAsync(dto, ct)));

        [HttpPut("kartlar/{id:int}")]
        public Task<IActionResult> UpdateKart(int id, [FromBody] MevzuatKartSaveDto dto, CancellationToken ct)
            => Calistir(async () => Sonuc(await _service.UpdateKartAsync(id, dto, ct)));

        [HttpDelete("kartlar/{id:int}")]
        public async Task<IActionResult> DeleteKart(int id, CancellationToken ct)
            => await _service.DeleteKartAsync(id, ct) ? NoContent() : NotFound();

        // ---- Bağlantı ----

        [HttpPost("baglantilar")]
        public Task<IActionResult> CreateBaglanti([FromBody] MevzuatBaglantiSaveDto dto, CancellationToken ct)
            => Calistir(async () => Ok(await _service.CreateBaglantiAsync(dto, ct)));

        [HttpDelete("baglantilar/{id:int}")]
        public async Task<IActionResult> DeleteBaglanti(int id, CancellationToken ct)
            => await _service.DeleteBaglantiAsync(id, ct) ? NoContent() : NotFound();

        // ---- Dışlama ----

        [HttpPost("dislamalar")]
        public Task<IActionResult> CreateDislama([FromBody] MevzuatDislamaSaveDto dto, CancellationToken ct)
            => Calistir(async () => Ok(await _service.CreateDislamaAsync(dto, ct)));

        [HttpDelete("dislamalar/{id:int}")]
        public async Task<IActionResult> DeleteDislama(int id, CancellationToken ct)
            => await _service.DeleteDislamaAsync(id, ct) ? NoContent() : NotFound();

        // ---- Tuval ----

        [HttpPost("tuvaller")]
        public Task<IActionResult> CreateTuval([FromBody] MevzuatTuvalSaveDto dto, CancellationToken ct)
            => Calistir(async () => Ok(await _service.CreateTuvalAsync(dto, ct)));

        [HttpPut("tuvaller/{id:int}")]
        public Task<IActionResult> UpdateTuval(int id, [FromBody] MevzuatTuvalSaveDto dto, CancellationToken ct)
            => Calistir(async () => Sonuc(await _service.UpdateTuvalAsync(id, dto, ct)));

        [HttpPost("tuvaller/{id:int}/kartlar")]
        public Task<IActionResult> TuvaleKartEkle(int id, [FromBody] MevzuatYerlesimDto dto, CancellationToken ct)
            => Calistir(async () => Sonuc(await _service.TuvaleKartEkleAsync(id, dto, ct)));

        [HttpDelete("tuvaller/{id:int}/kartlar/{kartId:int}")]
        public async Task<IActionResult> TuvaldenKartKaldir(int id, int kartId, CancellationToken ct)
            => await _service.TuvaldenKartKaldirAsync(id, kartId, ct) ? NoContent() : NotFound();

        [HttpPut("tuvaller/{id:int}/yerlesim")]
        public Task<IActionResult> YerlesimGuncelle(int id, [FromBody] List<MevzuatYerlesimDto> liste, CancellationToken ct)
            => Calistir(async () => Sonuc(await _service.YerlesimGuncelleAsync(id, liste, ct)));

        // ---- Şart takip ----

        [HttpPut("sart-takip")]
        public Task<IActionResult> SartTakipKaydet([FromBody] MevzuatSartTakipDto dto, CancellationToken ct)
            => Calistir(async () => Ok(await _service.SartTakipKaydetAsync(dto, ct)));

        // ---- Yardımcılar ----

        private IActionResult Sonuc<T>(T? deger) where T : class
            => deger is null ? NotFound() : Ok(deger);

        private async Task<IActionResult> Calistir(Func<Task<IActionResult>> islem)
        {
            try
            {
                return await islem();
            }
            catch (MevzuatGrafigiHatasi ex) when (ex.Bulunamadi)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (MevzuatGrafigiHatasi ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
