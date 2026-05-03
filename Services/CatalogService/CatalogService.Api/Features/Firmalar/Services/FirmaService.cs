using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Firmalar.Dtos;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Firmalar.Services
{
    public class FirmaService : IFirmaService
    {
        private readonly CatalogContext _context;

        public FirmaService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<List<FirmaDto>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.Firmalar.AsNoTracking();
            if (!includeInactive)
                query = query.Where(x => x.Aktif);

            return await query
                .OrderBy(x => x.KisaAd)
                .Select(x => new FirmaDto
                {
                    Id = x.Id,
                    VergiKimlikNo = x.VergiKimlikNo,
                    Unvan = x.Unvan,
                    KisaAd = x.KisaAd,
                    Email = x.Email,
                    Telefon = x.Telefon,
                    TicaretSicilNo = x.TicaretSicilNo,
                    VergiDairesi = x.VergiDairesi,
                    Aktif = x.Aktif,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<FirmaDto?> GetByIdAsync(int id)
        {
            var firma = await _context.Firmalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return firma is null ? null : ToDto(firma);
        }

        public async Task<FirmaDto> CreateAsync(FirmaCreateDto dto)
        {
            var vkn = dto.VergiKimlikNo.Trim();

            var exists = await _context.Firmalar.AnyAsync(x => x.VergiKimlikNo == vkn);
            if (exists)
                throw new DuplicateRecordException(nameof(Firma.VergiKimlikNo), $"Bu VergiKimlikNo zaten kayıtlı: {vkn}");

            var firma = new Firma
            {
                VergiKimlikNo = vkn,
                Unvan = dto.Unvan.Trim(),
                KisaAd = dto.KisaAd.Trim(),
                Email = dto.Email.Trim(),
                Telefon = dto.Telefon.Trim(),
                TicaretSicilNo = dto.TicaretSicilNo.Trim(),
                VergiDairesi = dto.VergiDairesi.Trim(),
                Aktif = dto.Aktif,
                CreatedAt = DateTime.UtcNow
            };

            _context.Firmalar.Add(firma);
            await _context.SaveChangesAsync();

            return ToDto(firma);
        }

        public async Task<FirmaDto?> UpdateAsync(int id, FirmaUpdateDto dto)
        {
            var firma = await _context.Firmalar.FirstOrDefaultAsync(x => x.Id == id);
            if (firma is null) return null;

            var vkn = dto.VergiKimlikNo.Trim();

            if (firma.VergiKimlikNo != vkn)
            {
                var exists = await _context.Firmalar.AnyAsync(x => x.VergiKimlikNo == vkn && x.Id != id);
                if (exists)
                    throw new DuplicateRecordException(nameof(Firma.VergiKimlikNo), $"Bu VergiKimlikNo zaten kayıtlı: {vkn}");
            }

            firma.VergiKimlikNo = vkn;
            firma.Unvan = dto.Unvan.Trim();
            firma.KisaAd = dto.KisaAd.Trim();
            firma.Email = dto.Email.Trim();
            firma.Telefon = dto.Telefon.Trim();
            firma.TicaretSicilNo = dto.TicaretSicilNo.Trim();
            firma.VergiDairesi = dto.VergiDairesi.Trim();
            firma.Aktif = dto.Aktif;
            firma.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ToDto(firma);
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var firma = await _context.Firmalar.FirstOrDefaultAsync(x => x.Id == id);
            if (firma is null) return false;

            if (!firma.Aktif) return true;

            firma.Aktif = false;
            firma.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static FirmaDto ToDto(Firma f) => new()
        {
            Id = f.Id,
            VergiKimlikNo = f.VergiKimlikNo,
            Unvan = f.Unvan,
            KisaAd = f.KisaAd,
            Email = f.Email,
            Telefon = f.Telefon,
            TicaretSicilNo = f.TicaretSicilNo,
            VergiDairesi = f.VergiDairesi,
            Aktif = f.Aktif,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }
}
