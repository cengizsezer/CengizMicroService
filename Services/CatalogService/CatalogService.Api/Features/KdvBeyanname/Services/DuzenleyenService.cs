using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Features.KdvBeyanname.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.KdvBeyanname.Services
{
    public class DuzenleyenService : IDuzenleyenService
    {
        private readonly CatalogContext _db;

        public DuzenleyenService(CatalogContext db) => _db = db;

        public async Task<List<DuzenleyenDto>> ListAsync(bool includeInactive, CancellationToken ct)
        {
            var q = _db.Duzenleyenler.AsNoTracking();
            if (!includeInactive)
                q = q.Where(d => d.Aktif);
            return await q.OrderBy(d => d.Kisaltma).Select(ToDtoExpr).ToListAsync(ct);
        }

        public async Task<DuzenleyenDto?> GetAsync(int id, CancellationToken ct)
        {
            var d = await _db.Duzenleyenler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return d is null ? null : ToDto(d);
        }

        public async Task<DuzenleyenDto> CreateAsync(DuzenleyenCreateDto dto, CancellationToken ct)
        {
            var entity = new Duzenleyen
            {
                Kisaltma       = dto.Kisaltma.Trim(),
                Vkn            = dto.Vkn.Trim(),
                Soyadi         = dto.Soyadi?.Trim(),
                Adi            = dto.Adi?.Trim(),
                TicaretSicilNo = dto.TicaretSicilNo?.Trim(),
                Eposta         = dto.Eposta?.Trim(),
                AlanKodu       = dto.AlanKodu?.Trim(),
                TelNo          = dto.TelNo?.Trim(),
                Aktif          = dto.Aktif,
                CreatedAt      = DateTime.UtcNow
            };
            _db.Duzenleyenler.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<DuzenleyenDto?> UpdateAsync(int id, DuzenleyenUpdateDto dto, CancellationToken ct)
        {
            var entity = await _db.Duzenleyenler.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return null;

            entity.Kisaltma       = dto.Kisaltma.Trim();
            entity.Vkn            = dto.Vkn.Trim();
            entity.Soyadi         = dto.Soyadi?.Trim();
            entity.Adi            = dto.Adi?.Trim();
            entity.TicaretSicilNo = dto.TicaretSicilNo?.Trim();
            entity.Eposta         = dto.Eposta?.Trim();
            entity.AlanKodu       = dto.AlanKodu?.Trim();
            entity.TelNo          = dto.TelNo?.Trim();
            entity.Aktif          = dto.Aktif;
            entity.UpdatedAt      = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct)
        {
            var entity = await _db.Duzenleyenler.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return false;
            if (!entity.Aktif) return true;

            entity.Aktif    = false;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ── mapping ────────────────────────────────────────────────────────

        private static DuzenleyenDto ToDto(Duzenleyen d) => new()
        {
            Id             = d.Id,
            Kisaltma       = d.Kisaltma,
            Vkn            = d.Vkn,
            Soyadi         = d.Soyadi,
            Adi            = d.Adi,
            TicaretSicilNo = d.TicaretSicilNo,
            Eposta         = d.Eposta,
            AlanKodu       = d.AlanKodu,
            TelNo          = d.TelNo,
            Aktif          = d.Aktif,
            CreatedAt      = d.CreatedAt,
            UpdatedAt      = d.UpdatedAt
        };

        // EF projection — IQueryable.Select için.
        private static readonly System.Linq.Expressions.Expression<Func<Duzenleyen, DuzenleyenDto>> ToDtoExpr =
            d => new DuzenleyenDto
            {
                Id             = d.Id,
                Kisaltma       = d.Kisaltma,
                Vkn            = d.Vkn,
                Soyadi         = d.Soyadi,
                Adi            = d.Adi,
                TicaretSicilNo = d.TicaretSicilNo,
                Eposta         = d.Eposta,
                AlanKodu       = d.AlanKodu,
                TelNo          = d.TelNo,
                Aktif          = d.Aktif,
                CreatedAt      = d.CreatedAt,
                UpdatedAt      = d.UpdatedAt
            };
    }
}
