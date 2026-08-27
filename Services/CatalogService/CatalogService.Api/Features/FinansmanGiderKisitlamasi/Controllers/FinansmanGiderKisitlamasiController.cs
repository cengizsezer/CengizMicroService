using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Dtos;
using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi.Controllers
{
    /// <summary>
    /// Finansman gider kısıtlaması uçları. Ekranı Hesaplamalar > Finansman Gider Kısıtlaması.
    /// Gateway'in genel <c>/catalog/{everything}</c> rotasından (Bearer'lı) geçer; servisin
    /// global yetki politikası olmadığı için korumayı controller'ın kendi
    /// <c>[Authorize]</c> bayrağı sağlar (bkz. KARARLAR §78).
    /// </summary>
    [ApiController]
    [Route("api/catalog/finansman-gider-kisitlamasi")]
    [Authorize]
    public class FinansmanGiderKisitlamasiController : ControllerBase
    {
        private readonly IFinansmanGiderKisitlamasiService _service;

        public FinansmanGiderKisitlamasiController(IFinansmanGiderKisitlamasiService service)
            => _service = service;

        [HttpPost("hesapla")]
        public async Task<ActionResult<FinansmanKisitlamaSonucDto>> Hesapla(
            [FromBody] FinansmanKisitlamaHesapRequest request, CancellationToken ct)
        {
            if (request.Ozsermaye is null)
                return BadRequest(new { message = "Özsermaye tutarı zorunlu." });

            try
            {
                return Ok(await _service.HesaplaAsync(request, ct));
            }
            catch (FinansmanKisitlamaOraniYokException ex)
            {
                // Oran mevzuattan gelir; eksikse tahmin edilmez, kullanıcıya söylenir.
                return BadRequest(new { message = ex.Message });
            }
        }

        // ---- Kısıtlama oranı (yıl bazlı) ----

        [HttpGet("oranlar")]
        public async Task<ActionResult<List<FinansmanKisitlamaOraniDto>>> GetOranlar(CancellationToken ct)
            => Ok(await _service.GetOranlarAsync(ct));

        [HttpGet("oranlar/{yil:int}")]
        public async Task<ActionResult<FinansmanKisitlamaOraniDto>> GetOran(int yil, CancellationToken ct)
        {
            var dto = await _service.GetOranAsync(yil, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPut("oranlar/{yil:int}")]
        public async Task<ActionResult<FinansmanKisitlamaOraniDto>> UpsertOran(
            int yil, [FromBody] FinansmanKisitlamaOraniSaveDto dto, CancellationToken ct)
        {
            if (yil < 2000 || yil > 2100)
                return BadRequest(new { message = "Yıl 2000–2100 aralığında olmalı." });

            if (dto.Oran < 0 || dto.Oran > 100)
                return BadRequest(new { message = "Kısıtlama oranı 0–100 aralığında olmalı (yüzde)." });

            return Ok(await _service.UpsertOranAsync(yil, dto, ct));
        }

        [HttpDelete("oranlar/{yil:int}")]
        public async Task<IActionResult> DeleteOran(int yil, CancellationToken ct)
            => await _service.DeleteOranAsync(yil, ct) ? NoContent() : NotFound();
    }
}
