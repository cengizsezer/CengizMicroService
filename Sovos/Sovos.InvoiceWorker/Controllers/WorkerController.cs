using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        _ = Task.Run(async () =>
        {
            using var bgScope = _scopeFactory.CreateScope();
            var orchestrator = bgScope.ServiceProvider
                .GetRequiredService<IInvoiceOrchestrator>();
            try
            {
                _logger.LogInformation("RunNow (manuel) başladı (CompanyId={Id})", companyId);
                await orchestrator.RunForCompanyAsync(companyId, manualMode: true, CancellationToken.None);
                _logger.LogInformation("RunNow (manuel) bitti (CompanyId={Id})", companyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RunNow hata (CompanyId={Id})", companyId);
            }
        });

        return Accepted(new { companyId, status = "queued" });
    }
}
