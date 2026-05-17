using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.IncomingInvoiceWorker.Services;

public interface IIncomingInvoiceScraper
{
    Task<List<ScrapedInvoice>> FetchIncomingInvoicesAsync(
        Company company,
        string decryptedPassword,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct);
}
