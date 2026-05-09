using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Interfaces;
using Sovos.InvoiceWorker.Data;

namespace Sovos.InvoiceWorker.Controllers;

[ApiController]
[Route("api/worker")]
public class WorkerController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerController> _logger;

    public WorkerController(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkerController> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", service = "Sovos.InvoiceWorker" });

    /// <summary>
    /// Belirli bir firma için manuel anlık tarama tetikler. Manuel modda Sovos'taki TÜM
    /// onay bekleyen faturalar için mail gönderilir (NotifiedAt bypass). Fire-and-forget.
    /// </summary>
    [HttpPost("run-now/{companyId:int}")]
    public async Task<IActionResult> RunNow(int companyId, CancellationToken ct)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SovosDbContext>();
            var exists = await db.Companies.AnyAsync(c => c.Id == companyId, ct);
            if (!exists) return NotFound(new { companyId, message = "Firma bulunamadı" });
        }

        var today = DateTime.Today;
        var fromDate = today.AddMonths(-1);
        var toDate = today;

        _ = Task.Run(async () =>
        {
            using var bgScope = _scopeFactory.CreateScope();
            var orchestrator = bgScope.ServiceProvider
                .GetRequiredService<IInvoiceOrchestrator>();
            try
            {
                _logger.LogInformation("RunNow (manuel) başladı (CompanyId={Id})", companyId);
                await orchestrator.RunForCompanyAsync(
                    companyId, manualMode: true, fromDate, toDate, CancellationToken.None);
                _logger.LogInformation("RunNow (manuel) bitti (CompanyId={Id})", companyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RunNow hata (CompanyId={Id})", companyId);
            }
        });

        return Accepted(new { companyId, status = "queued" });
    }

    /// <summary>
    /// Birden fazla firma için seçili tarih aralığında manuel toplu tarama.
    /// Senkron çalışır — sequential, firmalar arası 5sn gecikme. Bir firma fail olursa
    /// diğerleri devam eder. Sonuç olarak başarılı/başarısız listesi döner.
    /// </summary>
    [HttpPost("run-batch")]
    public async Task<ActionResult<BatchRunResult>> RunBatch(
        [FromBody] WorkerBatchRunRequest req, CancellationToken ct)
    {
        if (req is null || req.CompanyIds is null || req.CompanyIds.Count == 0)
            return BadRequest(new { message = "companyIds boş olamaz" });

        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IInvoiceOrchestrator>();

        try
        {
            _logger.LogInformation(
                "RunBatch başladı: {Count} firma, {From:yyyy-MM-dd} - {To:yyyy-MM-dd}",
                req.CompanyIds.Count, req.FromDate, req.ToDate);

            var result = await orchestrator.RunBatchAsync(
                req.CompanyIds, req.FromDate, req.ToDate, ct);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunBatch genel hata");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = ex.Message });
        }
    }
}

public class WorkerBatchRunRequest
{
    public List<int> CompanyIds { get; set; } = new();
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}
