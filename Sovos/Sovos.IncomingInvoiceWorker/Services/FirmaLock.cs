using System.Collections.Concurrent;

namespace Sovos.IncomingInvoiceWorker.Services;

/// <summary>
/// In-process, firma bazlı <see cref="SemaphoreSlim"/> kilidi (singleton).
/// NOT: Yalnızca BU process (IncomingInvoiceWorker) içindeki çağrıları serileştirir.
/// Zamanlanmış tarama ayrı bir process'te (Sovos.InvoiceWorker) çalıştığından
/// cross-process koruma sağlamaz; o senaryo için DB-tabanlı kilit gerekir.
/// </summary>
public class FirmaLock : IFirmaLock
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(int firmaId, CancellationToken ct)
    {
        var sem = _locks.GetOrAdd(firmaId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        return new Releaser(sem);
    }

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _sem;
        public Releaser(SemaphoreSlim sem) => _sem = sem;
        public void Dispose()
        {
            _sem?.Release();
            _sem = null;
        }
    }
}
