using Sovos.InvoiceWorker.Core.DTOs;

namespace Sovos.IncomingInvoiceWorker.Services;

public interface IGelenFaturaUpsertService
{
    /// <summary>
    /// Scrape edilmiş faturaları GelenFaturalar tablosuna idempotent şekilde yazar.
    /// (FirmaId, FaturaNo, GondericiVkn) unique key üzerinden upsert yapılır.
    /// </summary>
    /// <returns>Yeni eklenen + güncellenen toplam satır sayısı.</returns>
    Task<int> UpsertAsync(int firmaId, IReadOnlyList<ScrapedInvoice> scraped, CancellationToken ct);
}
