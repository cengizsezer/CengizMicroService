using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sovos.IncomingInvoiceWorker.Services;

namespace Sovos.IncomingInvoiceWorker.Controllers;

[ApiController]
[Route("api/kdv-beyanname")]
public class IncomingInvoiceController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncomingInvoiceController> _logger;

    public IncomingInvoiceController(
        IServiceScopeFactory scopeFactory,
        ILogger<IncomingInvoiceController> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new
    {
        status = "ok",
        service = "Sovos.IncomingInvoiceWorker"
    });

    /// <summary>
    /// Belirli bir firma için tarih aralığında DP'den gelen faturaları tarar.
    /// Fire-and-forget; KdvBeyannameTarama kaydı üzerinden ilerleme izlenebilir.
    /// </summary>
    [HttpPost("{firmaId:int}/tara")]
    public async Task<IActionResult> Tara(
        int firmaId,
        [FromBody] TaramaRequest req,
        CancellationToken ct)
    {
        if (req is null)
            return BadRequest(new { message = "İstek gövdesi boş olamaz." });

        if (req.BaslangicTarihi > req.BitisTarihi)
            return BadRequest(new { message = "Başlangıç tarihi bitiş tarihinden büyük olamaz." });

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
            var exists = await db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                return NotFound(new { firmaId, message = "Firma bulunamadı." });
        }

        _ = Task.Run(async () =>
        {
            using var bgScope = _scopeFactory.CreateScope();
            var orchestrator = bgScope.ServiceProvider
                .GetRequiredService<IIncomingInvoiceOrchestrator>();
            try
            {
                _logger.LogInformation(
                    "Tara başladı (FirmaId={Id}, {From:yyyy-MM-dd} - {To:yyyy-MM-dd})",
                    firmaId, req.BaslangicTarihi, req.BitisTarihi);

                await orchestrator.RunForFirmaAsync(
                    firmaId, req.BaslangicTarihi, req.BitisTarihi, CancellationToken.None);

                _logger.LogInformation("Tara bitti (FirmaId={Id})", firmaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tara hatası (FirmaId={Id})", firmaId);
            }
        });

        return Accepted(new
        {
            firmaId,
            status = "queued",
            mesaj = "Tarama başlatıldı. Sonucu /api/kdv-beyanname/{firmaId}/tarama-durumu ile sorgulayabilirsiniz."
        });
    }
}

public class TaramaRequest
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
}
