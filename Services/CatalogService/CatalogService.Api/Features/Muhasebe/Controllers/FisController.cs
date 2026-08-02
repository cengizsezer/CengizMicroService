using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Muhasebe.Controllers
{
    /// <summary>
    /// Fiş (yevmiye) uçları. FirmaId (TenantNo) token'dan gelir, request body'sinden değil;
    /// tenant izolasyonu <c>CatalogContext</c> query filter'ları ile sağlanır.
    /// İş kuralları (10–17) servistedir; buradaki kontroller yalnızca yönlendirme içindir.
    /// </summary>
    [ApiController]
    [Route("api/catalog/muhasebe/fis")]
    [Authorize]
    public class FisController : ControllerBase
    {
        private readonly IFisService _service;

        public FisController(IFisService service) => _service = service;

        // ---- Okuma ----

        /// <summary>Tarih aralığı, durum ve hesap filtresiyle fiş listesi.</summary>
        [HttpGet]
        public async Task<ActionResult<List<FisOzetDto>>> GetListe(
            [FromQuery] DateTime? bas,
            [FromQuery] DateTime? bit,
            [FromQuery] FisDurum? durum,
            [FromQuery] int? hesapId,
            CancellationToken ct)
        {
            var filtre = new FisFiltreDto { Bas = bas, Bit = bit, Durum = durum, HesapId = hesapId };
            return Ok(await _service.GetListeAsync(filtre, ct));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<FisDto>> GetById(int id, CancellationToken ct)
        {
            var dto = await _service.GetByIdAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        // ---- Yazma ----

        /// <summary>Yeni fiş; <c>kesinlestir</c> alanına göre taslak veya kesinleşmiş kaydedilir.</summary>
        [HttpPost]
        public async Task<ActionResult<FisDto>> Create([FromBody] FisYazDto dto, CancellationToken ct)
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
            catch (MuhasebeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Yalnızca taslak fiş güncellenir (iş kuralı 15).</summary>
        [HttpPut("{id:int}")]
        public async Task<ActionResult<FisDto>> Update(int id, [FromBody] FisYazDto dto, CancellationToken ct)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto, ct);
                return updated is null ? NotFound() : Ok(updated);
            }
            catch (MuhasebeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Taslak fişi kesinleştirir; kesinleşen fiş bir daha güncellenemez/silinemez.</summary>
        [HttpPatch("{id:int}/kesinlestir")]
        public async Task<ActionResult<FisDto>> Kesinlestir(int id, CancellationToken ct)
        {
            try
            {
                var dto = await _service.KesinlestirAsync(id, ct);
                return dto is null ? NotFound() : Ok(dto);
            }
            catch (MuhasebeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }

        /// <summary>Yalnızca taslak fiş silinir (iş kuralı 15).</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var sonuc = await _service.DeleteAsync(id, ct);
            return sonuc switch
            {
                FisSilmeSonuc.Silindi => NoContent(),
                FisSilmeSonuc.Kesinlesmis => Conflict(new
                {
                    message = "Kesinleşmiş fiş silinemez. Düzeltme için ters kayıt fişi oluşturun."
                }),
                _ => NotFound()
            };
        }

        /// <summary>Kesinleşmiş fişin borç/alacağını yer değiştirmiş yeni fişini üretir.</summary>
        [HttpPost("{id:int}/ters-kayit")]
        public async Task<ActionResult<FisDto>> TersKayit(int id, [FromBody] TersKayitDto? dto, CancellationToken ct)
        {
            try
            {
                var created = await _service.TersKayitAsync(id, dto ?? new TersKayitDto(), ct);
                return created is null
                    ? NotFound(new { message = "Ters kaydı alınacak fiş bulunamadı." })
                    : CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (MuhasebeKuralException ex)
            {
                return BadRequest(new { field = ex.Field, message = ex.Message });
            }
        }
    }
}
