using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface IMailSender
{
    Task SendNewInvoicesAsync(
        Company company, List<Invoice> newInvoices, CancellationToken ct);

    Task SendDailySummaryAsync(
        Company company, List<ScrapedInvoice> allPending, CancellationToken ct);
}
