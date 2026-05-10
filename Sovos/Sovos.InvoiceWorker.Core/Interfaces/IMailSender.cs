using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.InvoiceWorker.Core.Interfaces;

public interface IMailSender
{
    Task SendNewInvoicesAsync(
        Company company, List<Invoice> newInvoices, CancellationToken ct);

    // NOT: SendDailySummaryAsync şu an kullanılmıyor.
    // Daily/Hourly aynı 4 senaryolu mantığa geçti (10.05.2026). Gelecekte gerekirse tekrar açılabilir.
    Task SendDailySummaryAsync(
        Company company, List<ScrapedInvoice> allPending, CancellationToken ct);

    Task SendNoNewInvoicesAsync(
        Company company,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default);

    Task SendLoginErrorAsync(
        Company company,
        string errorMessage,
        CancellationToken ct = default);

    Task SendGeneralErrorAsync(
        Company company,
        string errorMessage,
        CancellationToken ct = default);
}
