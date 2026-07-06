namespace Sovos.IncomingInvoiceWorker.Services;

/// <summary>
/// Firma (≈ DP hesabı) bazlı seri erişim. Aynı firmaya ait iki Playwright oturumunun
/// (manuel PDF çekimi ↔ tarama) aynı anda açılıp Sovos oturumunu çakıştırmasını önler.
/// </summary>
public interface IFirmaLock
{
    /// <summary>
    /// Firma için kilidi alır; dönen <see cref="IDisposable"/> dispose edilene kadar
    /// aynı firma için başka çağrılar bekler.
    /// </summary>
    Task<IDisposable> AcquireAsync(int firmaId, CancellationToken ct);
}
