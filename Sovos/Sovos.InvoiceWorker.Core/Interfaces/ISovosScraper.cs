using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface ISovosScraper
{
    Task<List<ScrapedInvoice>> FetchPendingInvoicesAsync(
        Company company, string decryptedPassword, CancellationToken ct);
}
