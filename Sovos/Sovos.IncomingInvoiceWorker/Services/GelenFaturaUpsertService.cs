using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovos.InvoiceWorker.Core.DTOs;

namespace Sovos.IncomingInvoiceWorker.Services;

public class GelenFaturaUpsertService : IGelenFaturaUpsertService
{
    private static readonly decimal[] KdvOranSnapTargets = { 1m, 8m, 10m, 18m, 20m };

    private readonly CatalogContext _db;
    private readonly ILogger<GelenFaturaUpsertService> _logger;

    public GelenFaturaUpsertService(
        CatalogContext db,
        ILogger<GelenFaturaUpsertService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> UpsertAsync(
        int firmaId, IReadOnlyList<ScrapedInvoice> scraped, CancellationToken ct)
    {
        if (scraped.Count == 0)
        {
            _logger.LogInformation("FirmaId {FirmaId}: Scrape sonucu boş, upsert atlandı.", firmaId);
            return 0;
        }

        var keys = scraped
            .Select(s => new { s.FaturaNo, s.GondericiVkn })
            .ToList();

        var faturaNolar = keys.Select(k => k.FaturaNo).Distinct().ToList();
        var existing = await _db.GelenFaturalar
            .Where(g => g.FirmaId == firmaId && faturaNolar.Contains(g.FaturaNo))
            .ToListAsync(ct);

        var existingMap = existing
            .ToDictionary(g => (g.FaturaNo, g.GondericiVkn), g => g);

        var now = DateTime.UtcNow;
        int inserted = 0, updated = 0;

        foreach (var s in scraped)
        {
            var matrah   = s.FaturaTutari - s.ToplamVergi;
            var kdvOrani = ComputeKdvOrani(matrah, s.ToplamVergi);
            var key      = (s.FaturaNo, s.GondericiVkn);

            if (existingMap.TryGetValue(key, out var existingRow))
            {
                existingRow.GondericiUnvan = s.FirmaUnvani;
                existingRow.ParaBirimi     = s.ParaBirimi;
                existingRow.FaturaTarihi   = s.DuzenlenmeTarihi;
                existingRow.MatrahTutari   = matrah;
                existingRow.KdvTutari      = s.ToplamVergi;
                existingRow.ToplamTutar    = s.FaturaTutari;
                existingRow.KdvOrani       = kdvOrani;
                existingRow.Statu          = s.Statu;
                existingRow.SonGuncelleme  = now;
                updated++;
            }
            else
            {
                _db.GelenFaturalar.Add(new GelenFatura
                {
                    FirmaId        = firmaId,
                    FaturaNo       = s.FaturaNo,
                    GondericiVkn   = s.GondericiVkn,
                    GondericiUnvan = s.FirmaUnvani,
                    ParaBirimi     = s.ParaBirimi,
                    FaturaTarihi   = s.DuzenlenmeTarihi,
                    MatrahTutari   = matrah,
                    KdvTutari      = s.ToplamVergi,
                    ToplamTutar    = s.FaturaTutari,
                    KdvOrani       = kdvOrani,
                    Statu          = s.Statu,
                    SonGuncelleme  = now,
                    Kaynak         = "DP"
                });
                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "FirmaId {FirmaId}: GelenFaturalar upsert — {Inserted} yeni, {Updated} güncellendi (toplam {Total}).",
            firmaId, inserted, updated, scraped.Count);

        return inserted + updated;
    }

    // Matrah 0 ise oran tanımsız → null. Aksi halde (kdv / matrah)*100 hesaplanıp
    // 1 / 8 / 10 / 18 / 20 listesinde en yakın değere snap edilir.
    private static decimal? ComputeKdvOrani(decimal matrah, decimal kdv)
    {
        if (matrah == 0m) return null;
        var raw = kdv / matrah * 100m;
        return KdvOranSnapTargets.OrderBy(t => Math.Abs(t - raw)).First();
    }
}
