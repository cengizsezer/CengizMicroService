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

    /// <summary>
    /// Tek bir faturanın PDF'ini DP portalından canlı çeker. Login + Inbox
    /// navigasyonu + tarih filtresi <see cref="FetchIncomingInvoicesAsync"/> ile
    /// aynı yardımcıları kullanır; ardından grid'de <paramref name="faturaNo"/>
    /// satırına sağ tıklayıp "Yazdır/İndir ▶ Tek Tek PDF İndir" ile ZIP indirir
    /// ve içindeki PDF'i çıkarır. <paramref name="fromDate"/>/<paramref name="toDate"/>
    /// faturanın grid'de listelenmesini sağlayacak aralık olmalı (örn. fatura tarihi
    /// ayının başı-sonu).
    /// </summary>
    Task<InvoicePdfResult> DownloadInvoicePdfAsync(
        Company company,
        string decryptedPassword,
        DateTime fromDate,
        DateTime toDate,
        string faturaNo,
        CancellationToken ct);
}
