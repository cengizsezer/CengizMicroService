using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface IBeyannameOzetService
    {
        Task<BeyannameOzetDto> GetAsync(int yil, int ay, bool pasifleriGoster = false, CancellationToken ct = default);
    }

    /// <summary>
    /// Özet matrisinin veri kaynağı. Sorgular burada, matrisin kuralları
    /// <see cref="BeyannameOzetKurucu"/>'da (saf fonksiyon, testleri veritabanısız).
    ///
    /// <b>Kapsam:</b> satırlar Firmalarım'dan (dbo.Firmalar; global, Firmalarım'la aynı kapsam) — eski
    /// dbo.CustomerCompanies artık okunmaz. Varsayılan yalnız AKTİF firmalar; <c>pasifleriGoster</c> ile pasifler
    /// de. Takip'le aynı kural (DeclarationQueryService.Gorunur): pasif firmanın kaydı yalnız istenirse, FirmaId'si
    /// boş kayıt her zaman ("Eşleşmeyen firma" satırı).
    /// </summary>
    public class BeyannameOzetService : IBeyannameOzetService
    {
        private readonly CatalogContext _db;

        public BeyannameOzetService(CatalogContext db) => _db = db;

        public async Task<BeyannameOzetDto> GetAsync(int yil, int ay, bool pasifleriGoster = false, CancellationToken ct = default)
        {
            if (yil < 2000 || yil > 2100)
                throw new BeyannameKuralException(nameof(yil), "Geçersiz yıl.");

            if (ay is < 1 or > 12)
                throw new BeyannameKuralException(nameof(ay), "Ay 1 ile 12 arasında olmalı.");

            var turler = await _db.BeyannameTurleri.AsNoTracking()
                .Where(t => t.Aktif)
                .OrderBy(t => t.Sira).ThenBy(t => t.Id)
                .ToListAsync(ct);

            var tr = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true);
            var hepsi = await BeyannameFirmasi.HepsiAsync(_db, ct);
            var firmalar = hepsi
                .Where(f => f.Aktif || pasifleriGoster)
                .OrderBy(f => f.Ad, tr)
                .ToList();

            var beyannameler = DeclarationQueryService.Gorunur(
                    await _db.Declarations.AsNoTracking().Where(d => d.Year == yil && d.Month == ay).ToListAsync(ct),
                    hepsi.ToDictionary(f => f.Id), pasifleriGoster)
                .ToList();

            var beyannameIdleri = beyannameler.Select(d => d.Id).ToList();

            var ekler = await _db.BeyannameEkleri.AsNoTracking()
                .Where(e => beyannameIdleri.Contains(e.DeclarationId))
                .ToListAsync(ct);

            return BeyannameOzetKurucu.Kur(yil, ay, turler, firmalar, beyannameler, ekler);
        }
    }
}
