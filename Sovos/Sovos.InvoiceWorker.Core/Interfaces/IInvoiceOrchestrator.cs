namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface IInvoiceOrchestrator
{
    Task RunHourlyCheckAsync(CancellationToken ct);
    Task RunDailySummaryAsync(CancellationToken ct);
    Task RunForCompanyAsync(int companyId, bool manualMode, CancellationToken ct);
    Task RunScheduledChecksAsync(CancellationToken ct);
}
