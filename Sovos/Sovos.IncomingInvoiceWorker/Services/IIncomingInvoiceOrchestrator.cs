namespace Sovos.IncomingInvoiceWorker.Services;

public interface IIncomingInvoiceOrchestrator
{
    /// <summary>
    /// Firma için DP'den gelen faturaları tarayıp GelenFaturalar tablosuna işler.
    /// Bir KdvBeyannameTarama kaydı oluşturur, ilerleme takibi sağlar.
    /// </summary>
    Task<long> RunForFirmaAsync(
        int firmaId, DateTime fromDate, DateTime toDate, CancellationToken ct);
}
