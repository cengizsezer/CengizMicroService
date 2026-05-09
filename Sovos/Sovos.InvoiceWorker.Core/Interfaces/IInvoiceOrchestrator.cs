using Sovos.InvoiceWorker.Core.DTOs;

namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface IInvoiceOrchestrator
{
    Task RunForCompanyAsync(
        int companyId,
        bool manualMode,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct);

    Task RunScheduledChecksAsync(CancellationToken ct);

    Task<BatchRunResult> RunBatchAsync(
        IReadOnlyList<int> companyIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct);
}
