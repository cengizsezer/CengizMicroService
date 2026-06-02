using CatalogService.Api.Features.Finans.Dtos;
using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Mukellefler.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Finans.Services
{
    public class FinansService : IFinansService
    {
        private const int MaxPageSize = 200;
        private readonly CatalogContext _context;

        public FinansService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<MukellefFinansDto>> GetPaginatedAsync(
            int page = 1,
            int pageSize = 50,
            string? search = null,
            FinansSinifi? finansSinifi = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var query = _context.Mukellefler.AsNoTracking().AsQueryable();

            if (finansSinifi.HasValue)
                query = query.Where(x => x.FinansSinifi == finansSinifi.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(x =>
                    EF.Functions.Like(x.Unvan, $"%{s}%") ||
                    EF.Functions.Like(x.VergiKimlikNo, $"%{s}%") ||
                    EF.Functions.Like(x.SozlesmeNo, $"%{s}%"));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(x => x.Unvan)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new MukellefFinansDto
                {
                    Id = x.Id,
                    SozlesmeNo = x.SozlesmeNo,
                    Unvan = x.Unvan,
                    VergiKimlikNo = x.VergiKimlikNo,
                    Durum = x.Durum,
                    FinansSinifi = x.FinansSinifi,
                    AcikBakiye = x.AcikBakiye,
                    SonOdemeTarihi = x.SonOdemeTarihi,
                    FinansAciklama = x.FinansAciklama
                })
                .ToListAsync();

            return new PagedResult<MukellefFinansDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MukellefFinansDto?> GetByIdAsync(int id)
        {
            var m = await _context.Mukellefler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return m is null ? null : ToDto(m);
        }

        public async Task<MukellefFinansDto?> UpdateFinansAsync(int id, FinansUpdateDto dto)
        {
            var mukellef = await _context.Mukellefler.FirstOrDefaultAsync(x => x.Id == id);
            if (mukellef is null) return null;

            mukellef.FinansSinifi = dto.FinansSinifi;
            mukellef.AcikBakiye = dto.AcikBakiye;
            mukellef.SonOdemeTarihi = dto.SonOdemeTarihi;
            mukellef.FinansAciklama = string.IsNullOrWhiteSpace(dto.FinansAciklama) ? null : dto.FinansAciklama.Trim();
            mukellef.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ToDto(mukellef);
        }

        private static MukellefFinansDto ToDto(Mukellef m) => new()
        {
            Id = m.Id,
            SozlesmeNo = m.SozlesmeNo,
            Unvan = m.Unvan,
            VergiKimlikNo = m.VergiKimlikNo,
            Durum = m.Durum,
            FinansSinifi = m.FinansSinifi,
            AcikBakiye = m.AcikBakiye,
            SonOdemeTarihi = m.SonOdemeTarihi,
            FinansAciklama = m.FinansAciklama
        };
    }
}
