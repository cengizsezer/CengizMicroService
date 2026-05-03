namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface IInvoiceOrchestrator
{
    Task RunForCompanyAsync(int companyId, bool manualMode, CancellationToken ct);
    Task RunScheduledChecksAsync(CancellationToken ct);
}
