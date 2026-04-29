using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface IInvoiceDiffService
{
    Task<List<Invoice>> SaveAndGetNewAsync(
        int companyId, List<ScrapedInvoice> scraped, CancellationToken ct);

    Task MarkAsNotifiedAsync(IEnumerable<long> invoiceIds, CancellationToken ct);
}
