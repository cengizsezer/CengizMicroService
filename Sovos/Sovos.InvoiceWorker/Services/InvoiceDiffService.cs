using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Interfaces;
using Sovos.InvoiceWorker.Data;

namespace Sovos.InvoiceWorker.Services;

public class InvoiceDiffService : IInvoiceDiffService
{
    private readonly SovosDbContext _db;
    private readonly ILogger<InvoiceDiffService> _logger;

    public InvoiceDiffService(SovosDbContext db, ILogger<InvoiceDiffService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Invoice>> SaveAndGetNewAsync(
        int companyId, List<ScrapedInvoice> scraped, CancellationToken ct,
        bool manualMode = false)
    {
        if (scraped.Count == 0)
        {
            _logger.LogInformation(
                "CompanyId {CompanyId}: Scrape edilmiş fatura yok, diff atlandı.", companyId);
            return new List<Invoice>();
        }

        // DB'deki tüm Invoice'ları al — NotifiedAt'a bakacağız (sadece keyler yetmez).
        var existing = await _db.Invoices
            .Where(i => i.CompanyId == companyId)
            .ToListAsync(ct);

        var existingMap = existing.ToDictionary(
            i => (i.FaturaNo, i.GondericiVkn),
            i => i);

        var now = DateTime.UtcNow;
        var newlyInserted = new List<Invoice>();
        var toNotify = new List<Invoice>();   // Mail edilecekler: yeni eklenenler + NotifiedAt=null retry'ları

        foreach (var s in scraped)
        {
            var key = (s.FaturaNo, s.GondericiVkn);

            if (existingMap.TryGetValue(key, out var existingInvoice))
            {
                // Manuel modda her şey mail edilir (NotifiedAt bypass).
                // Otomatik modda sadece NotifiedAt=null ise retry için mail kuyruğuna al.
                if (manualMode || existingInvoice.NotifiedAt == null)
                    toNotify.Add(existingInvoice);
                continue;
            }

            // Yeni kayıt — insert + mail kuyruğu.
            var fresh = new Invoice
            {
                CompanyId         = companyId,
                FaturaNo          = s.FaturaNo,
                GondericiVkn      = s.GondericiVkn,
                FirmaUnvani       = s.FirmaUnvani,
                ParaBirimi        = s.ParaBirimi,
                FaturaTutari      = s.FaturaTutari,
                ToplamVergi       = s.ToplamVergi,
                IskontoTutari     = s.IskontoTutari,
                Artirim           = s.Artirim,
                SiparisNo         = s.SiparisNo,
                SonOdemeTarihi    = s.SonOdemeTarihi,
                DuzenlenmeTarihi  = s.DuzenlenmeTarihi,
                OlusturulmaTarihi = s.OlusturulmaTarihi,
                FirstSeenAt       = now,
                NotifiedAt        = null
            };
            newlyInserted.Add(fresh);
            toNotify.Add(fresh);
        }

        if (newlyInserted.Count > 0)
        {
            await _db.Invoices.AddRangeAsync(newlyInserted, ct);
            await _db.SaveChangesAsync(ct);
        }

        var retryCount = toNotify.Count - newlyInserted.Count;
        var alreadyNotified = scraped.Count - toNotify.Count;
        _logger.LogInformation(
            "CompanyId {CompanyId} (manualMode={Manual}): {Total} scraped → {New} yeni eklendi, " +
            "{Retry} mail kuyruğunda mevcut, {Done} mail dışı",
            companyId, manualMode, scraped.Count, newlyInserted.Count, retryCount, alreadyNotified);

        return toNotify;
    }

    public async Task MarkAsNotifiedAsync(IEnumerable<long> invoiceIds, CancellationToken ct)
    {
        var ids = invoiceIds.ToList();
        if (ids.Count == 0)
            return;

        var rows = await _db.Invoices
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var r in rows)
            r.NotifiedAt = now;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "{Count} fatura NotifiedAt={Now:O} olarak işaretlendi", rows.Count, now);
    }
}
