using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Mukellefler.Dtos;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Mukellefler.Services
{
    public class MukellefService : IMukellefService
    {
        private const int MaxPageSize = 200;
        private readonly CatalogContext _context;

        public MukellefService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<MukellefDto>> GetPaginatedAsync(
            int page = 1,
            int pageSize = 50,
            string? search = null,
            MukellefDurumu? durum = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var query = _context.Mukellefler.AsNoTracking().AsQueryable();

            if (durum.HasValue)
                query = query.Where(x => x.Durum == durum.Value);

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
                .Select(x => new MukellefDto
                {
                    Id = x.Id,
                    SozlesmeNo = x.SozlesmeNo,
                    SozlesmeTarihi = x.SozlesmeTarihi,
                    Unvan = x.Unvan,
                    VergiKimlikNo = x.VergiKimlikNo,
                    Durum = x.Durum,
                    IptalFeshTarihi = x.IptalFeshTarihi,
                    Hizmet = x.Hizmet,
                    Telefon = x.Telefon,
                    Email = x.Email,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();

            return new PagedResult<MukellefDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MukellefDto?> GetByIdAsync(int id)
        {
            var m = await _context.Mukellefler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return m is null ? null : ToDto(m);
        }

        public async Task<MukellefDto> CreateAsync(MukellefCreateDto dto)
        {
            var sozlesmeNo = dto.SozlesmeNo.Trim();
            var vkn = dto.VergiKimlikNo.Trim();

            if (await _context.Mukellefler.AnyAsync(x => x.SozlesmeNo == sozlesmeNo))
                throw new DuplicateRecordException(nameof(Mukellef.SozlesmeNo), $"Bu SozlesmeNo zaten kayıtlı: {sozlesmeNo}");

            if (await _context.Mukellefler.AnyAsync(x => x.VergiKimlikNo == vkn))
                throw new DuplicateRecordException(nameof(Mukellef.VergiKimlikNo), $"Bu VergiKimlikNo zaten kayıtlı: {vkn}");

            var mukellef = new Mukellef
            {
                SozlesmeNo = sozlesmeNo,
                SozlesmeTarihi = dto.SozlesmeTarihi,
                Unvan = dto.Unvan.Trim(),
                VergiKimlikNo = vkn,
                Durum = dto.Durum,
                IptalFeshTarihi = dto.IptalFeshTarihi,
                Hizmet = dto.Hizmet.Trim(),
                Telefon = dto.Telefon.Trim(),
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Mukellefler.Add(mukellef);
            await _context.SaveChangesAsync();

            return ToDto(mukellef);
        }

        public async Task<MukellefDto?> UpdateAsync(int id, MukellefUpdateDto dto)
        {
            var mukellef = await _context.Mukellefler.FirstOrDefaultAsync(x => x.Id == id);
            if (mukellef is null) return null;

            var sozlesmeNo = dto.SozlesmeNo.Trim();
            var vkn = dto.VergiKimlikNo.Trim();

            if (mukellef.SozlesmeNo != sozlesmeNo)
            {
                if (await _context.Mukellefler.AnyAsync(x => x.SozlesmeNo == sozlesmeNo && x.Id != id))
                    throw new DuplicateRecordException(nameof(Mukellef.SozlesmeNo), $"Bu SozlesmeNo zaten kayıtlı: {sozlesmeNo}");
            }

            if (mukellef.VergiKimlikNo != vkn)
            {
                if (await _context.Mukellefler.AnyAsync(x => x.VergiKimlikNo == vkn && x.Id != id))
                    throw new DuplicateRecordException(nameof(Mukellef.VergiKimlikNo), $"Bu VergiKimlikNo zaten kayıtlı: {vkn}");
            }

            mukellef.SozlesmeNo = sozlesmeNo;
            mukellef.SozlesmeTarihi = dto.SozlesmeTarihi;
            mukellef.Unvan = dto.Unvan.Trim();
            mukellef.VergiKimlikNo = vkn;
            mukellef.Durum = dto.Durum;
            mukellef.IptalFeshTarihi = dto.IptalFeshTarihi;
            mukellef.Hizmet = dto.Hizmet.Trim();
            mukellef.Telefon = dto.Telefon.Trim();
            mukellef.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            mukellef.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ToDto(mukellef);
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var mukellef = await _context.Mukellefler.FirstOrDefaultAsync(x => x.Id == id);
            if (mukellef is null) return false;

            if (mukellef.Durum == MukellefDurumu.IptalEdildi) return true;

            mukellef.Durum = MukellefDurumu.IptalEdildi;
            mukellef.IptalFeshTarihi = DateTime.UtcNow;
            mukellef.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static MukellefDto ToDto(Mukellef m) => new()
        {
            Id = m.Id,
            SozlesmeNo = m.SozlesmeNo,
            SozlesmeTarihi = m.SozlesmeTarihi,
            Unvan = m.Unvan,
            VergiKimlikNo = m.VergiKimlikNo,
            Durum = m.Durum,
            IptalFeshTarihi = m.IptalFeshTarihi,
            Hizmet = m.Hizmet,
            Telefon = m.Telefon,
            Email = m.Email,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt
        };
    }
}
