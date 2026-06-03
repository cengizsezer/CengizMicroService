using CatalogService.Api.Features.Banka.Domain;
using CatalogService.Api.Features.Banka.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Banka.Services
{
    public class HesapService : IHesapService
    {
        private readonly CatalogContext _context;

        public HesapService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<List<HesapDto>> GetAllAsync(int? firmaId = null, bool includeInactive = false)
        {
            var query = _context.Hesaplar.AsNoTracking();

            if (!includeInactive)
                query = query.Where(x => x.AktifMi);

            if (firmaId is int fId)
                query = query.Where(x => x.FirmaId == fId);

            // FirmaAd için Firmalar ile join (KisaAd kullanıcıya gösterilen kısa addır).
            return await (
                from h in query
                join f in _context.Firmalar.AsNoTracking()
                    on h.FirmaId equals f.Id
                orderby f.KisaAd, h.Ad
                select new HesapDto
                {
                    Id        = h.Id,
                    FirmaId   = h.FirmaId,
                    FirmaAd   = f.KisaAd,
                    Tip       = h.Tip,
                    Ad        = h.Ad,
                    Siklik    = h.Siklik,
                    AktifMi   = h.AktifMi,
                    CreatedAt = h.CreatedAt,
                    UpdatedAt = h.UpdatedAt
                }
            ).ToListAsync();
        }

        public async Task<HesapDto?> GetByIdAsync(int id)
        {
            var hesap = await _context.Hesaplar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (hesap is null) return null;

            var firmaAd = await ResolveFirmaAd(hesap.FirmaId);
            return ToDto(hesap, firmaAd);
        }

        public async Task<HesapDto> CreateAsync(HesapCreateDto dto)
        {
            if (!await _context.Firmalar.AnyAsync(f => f.Id == dto.FirmaId))
                throw new ArgumentException($"Firma bulunamadı: Id={dto.FirmaId}");

            var hesap = new Hesap
            {
                FirmaId   = dto.FirmaId,
                Tip       = dto.Tip,
                Ad        = dto.Ad.Trim(),
                Siklik    = dto.Siklik,
                AktifMi   = dto.AktifMi,
                CreatedAt = DateTime.UtcNow
            };

            _context.Hesaplar.Add(hesap);
            await _context.SaveChangesAsync();

            var firmaAd = await ResolveFirmaAd(hesap.FirmaId);
            return ToDto(hesap, firmaAd);
        }

        public async Task<HesapDto?> UpdateAsync(int id, HesapUpdateDto dto)
        {
            var hesap = await _context.Hesaplar.FirstOrDefaultAsync(x => x.Id == id);
            if (hesap is null) return null;

            if (hesap.FirmaId != dto.FirmaId &&
                !await _context.Firmalar.AnyAsync(f => f.Id == dto.FirmaId))
            {
                throw new ArgumentException($"Firma bulunamadı: Id={dto.FirmaId}");
            }

            hesap.FirmaId   = dto.FirmaId;
            hesap.Tip       = dto.Tip;
            hesap.Ad        = dto.Ad.Trim();
            hesap.Siklik    = dto.Siklik;
            hesap.AktifMi   = dto.AktifMi;
            hesap.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var firmaAd = await ResolveFirmaAd(hesap.FirmaId);
            return ToDto(hesap, firmaAd);
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var hesap = await _context.Hesaplar.FirstOrDefaultAsync(x => x.Id == id);
            if (hesap is null) return false;

            if (!hesap.AktifMi) return true;

            hesap.AktifMi = false;
            hesap.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<string> ResolveFirmaAd(int firmaId)
        {
            return await _context.Firmalar.AsNoTracking()
                .Where(f => f.Id == firmaId)
                .Select(f => f.KisaAd)
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        private static HesapDto ToDto(Hesap h, string firmaAd) => new()
        {
            Id        = h.Id,
            FirmaId   = h.FirmaId,
            FirmaAd   = firmaAd,
            Tip       = h.Tip,
            Ad        = h.Ad,
            Siklik    = h.Siklik,
            AktifMi   = h.AktifMi,
            CreatedAt = h.CreatedAt,
            UpdatedAt = h.UpdatedAt
        };
    }
}
