using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface IBeyannameOzetService
    {
        Task<BeyannameOzetDto> GetAsync(int yil, int ay, CancellationToken ct = default);
    }

    /// <summary>
    /// Özet matrisinin veri kaynağı. Sorgular burada, matrisin kuralları
    /// <see cref="BeyannameOzetKurucu"/>'da (saf fonksiyon, testleri veritabanısız).
    ///
    /// <b>Kapsam:</b> satırlar <see cref="Entities.CustomerCompany"/> tablosundan gelir ve o
    /// tablonun tenant filtresi zaten var. Beyanname kayıtlarında filtre <b>yok</b> (modül
    /// baştan böyle kurulmuş); matris yalnız görünen firmaların kayıtlarını topladığı için
    /// dışarıdan bir kayıt sızmıyor. Beyanname tablosunun kendi kapsamı ayrı bir iş —
    /// bkz. KARARLAR §92.
    /// </summary>
    public class BeyannameOzetService : IBeyannameOzetService
    {
        private readonly CatalogContext _db;

        public BeyannameOzetService(CatalogContext db) => _db = db;

        public async Task<BeyannameOzetDto> GetAsync(int yil, int ay, CancellationToken ct = default)
        {
            if (yil < 2000 || yil > 2100)
                throw new BeyannameKuralException(nameof(yil), "Geçersiz yıl.");

            if (ay is < 1 or > 12)
                throw new BeyannameKuralException(nameof(ay), "Ay 1 ile 12 arasında olmalı.");

            var turler = await _db.BeyannameTurleri.AsNoTracking()
                .Where(t => t.Aktif)
                .OrderBy(t => t.Sira).ThenBy(t => t.Id)
                .ToListAsync(ct);

            var firmalar = await _db.CustomerCompanies.AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.CompanyName)
                .ToListAsync(ct);

            var firmaIdleri = firmalar.Select(f => f.Id).ToList();

            var beyannameler = await _db.Declarations.AsNoTracking()
                .Where(d => d.Year == yil && d.Month == ay && firmaIdleri.Contains(d.CustomerCompanyId))
                .ToListAsync(ct);

            var beyannameIdleri = beyannameler.Select(d => d.Id).ToList();

            var ekler = await _db.BeyannameEkleri.AsNoTracking()
                .Where(e => beyannameIdleri.Contains(e.DeclarationId))
                .ToListAsync(ct);

            return BeyannameOzetKurucu.Kur(yil, ay, turler, firmalar, beyannameler, ekler);
        }
    }
}
