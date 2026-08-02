using System.Linq.Expressions;
using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <inheritdoc cref="IMasrafMerkeziService"/>
    public class MasrafMerkeziService : IMasrafMerkeziService
    {
        private const int KodUzunluk = 10;
        private const int AdUzunluk = 100;

        private readonly CatalogContext _db;

        public MasrafMerkeziService(CatalogContext db) => _db = db;

        private static readonly Expression<Func<MasrafMerkezi, MasrafMerkeziDto>> Projeksiyon = m => new MasrafMerkeziDto
        {
            MasrafMerkeziId = m.Id,
            Kod = m.Kod,
            Ad = m.Ad,
            Aktif = m.Aktif
        };

        public Task<List<MasrafMerkeziDto>> GetHepsiAsync(bool pasifDahil = false, CancellationToken ct = default)
            => _db.MasrafMerkezleri
                  .AsNoTracking()
                  .Where(m => pasifDahil || m.Aktif)
                  .OrderBy(m => m.Kod)
                  .Select(Projeksiyon)
                  .ToListAsync(ct);

        public async Task<MasrafMerkeziDto?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _db.MasrafMerkezleri
                        .AsNoTracking()
                        .Where(m => m.Id == id)
                        .Select(Projeksiyon)
                        .FirstOrDefaultAsync(ct);

        public async Task<MasrafMerkeziDto> CreateAsync(MasrafMerkeziYazDto dto, CancellationToken ct = default)
        {
            var kod = (dto.Kod ?? string.Empty).Trim();
            var ad = (dto.Ad ?? string.Empty).Trim();

            if (kod.Length == 0)
                throw new MuhasebeKuralException("kod", "Masraf merkezi kodu boş bırakılamaz.");

            if (kod.Length > KodUzunluk)
                throw new MuhasebeKuralException("kod", $"Masraf merkezi kodu en fazla {KodUzunluk} karakter olabilir.");

            if (ad.Length == 0)
                throw new MuhasebeKuralException("ad", "Masraf merkezi adı boş bırakılamaz.");

            if (ad.Length > AdUzunluk)
                throw new MuhasebeKuralException("ad", $"Masraf merkezi adı en fazla {AdUzunluk} karakter olabilir.");

            // Pasif merkez de kodu tutar: aynı kod ikinci kez açılamaz, mevcut olan geri açılır.
            var mevcut = await _db.MasrafMerkezleri.AnyAsync(m => m.Kod == kod, ct);
            if (mevcut)
                throw new DuplicateRecordException("kod", $"\"{kod}\" kodlu masraf merkezi zaten var. Farklı bir kod girin.");

            var entity = new MasrafMerkezi { Kod = kod, Ad = ad, Aktif = true };

            _db.MasrafMerkezleri.Add(entity);
            await _db.SaveChangesAsync(ct);

            return new MasrafMerkeziDto
            {
                MasrafMerkeziId = entity.Id,
                Kod = entity.Kod,
                Ad = entity.Ad,
                Aktif = entity.Aktif
            };
        }

        public async Task<MasrafMerkeziDto?> PasifeAlAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.MasrafMerkezleri.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (entity is null) return null;

            if (entity.Aktif)
            {
                entity.Aktif = false;
                await _db.SaveChangesAsync(ct);
            }

            return new MasrafMerkeziDto
            {
                MasrafMerkeziId = entity.Id,
                Kod = entity.Kod,
                Ad = entity.Ad,
                Aktif = entity.Aktif
            };
        }
    }
}
