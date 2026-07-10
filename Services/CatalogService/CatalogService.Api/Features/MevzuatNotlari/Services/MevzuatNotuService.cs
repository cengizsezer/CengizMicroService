using CatalogService.Api.Features.MevzuatNotlari.Domain;
using CatalogService.Api.Features.MevzuatNotlari.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.MevzuatNotlari.Services
{
    public class MevzuatNotuService : IMevzuatNotuService
    {
        private readonly CatalogContext _db;

        public MevzuatNotuService(CatalogContext db) => _db = db;

        public async Task<List<MevzuatNotuDto>> GetAllAsync(string? kategori, string? arama, CancellationToken ct)
        {
            var query = _db.MevzuatNotlari.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(kategori))
            {
                var k = kategori.Trim();
                query = query.Where(x => x.Kategori == k);
            }

            if (!string.IsNullOrWhiteSpace(arama))
            {
                // Case-insensitive Contains: her iki tarafı da ToLower ile karşılaştır (collation'dan bağımsız).
                var a = arama.Trim().ToLower();
                query = query.Where(x =>
                    x.Baslik.ToLower().Contains(a)
                    || (x.MaddeNo != null && x.MaddeNo.ToLower().Contains(a))
                    || (x.Ozet != null && x.Ozet.ToLower().Contains(a))
                    || (x.Etiketler != null && x.Etiketler.ToLower().Contains(a)));
            }

            return await query
                .OrderByDescending(x => x.OlusturmaTarihi)
                .Select(x => new MevzuatNotuDto
                {
                    Id               = x.Id,
                    Kategori         = x.Kategori,
                    MaddeNo          = x.MaddeNo,
                    Baslik           = x.Baslik,
                    Ozet             = x.Ozet,
                    Icerik           = x.Icerik,
                    Etiketler        = x.Etiketler,
                    Kaynak           = x.Kaynak,
                    OlusturmaTarihi  = x.OlusturmaTarihi,
                    GuncellemeTarihi = x.GuncellemeTarihi,
                })
                .ToListAsync(ct);
        }

        public async Task<MevzuatNotuDto?> GetByIdAsync(int id, CancellationToken ct)
        {
            var entity = await _db.MevzuatNotlari
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return entity is null ? null : ToDto(entity);
        }

        public async Task<MevzuatNotuDto> CreateAsync(MevzuatNotuDto dto, CancellationToken ct)
        {
            var entity = new MevzuatNotu
            {
                Kategori         = dto.Kategori?.Trim() ?? string.Empty,
                MaddeNo          = dto.MaddeNo,
                Baslik           = dto.Baslik?.Trim() ?? string.Empty,
                Ozet             = dto.Ozet,
                Icerik           = dto.Icerik,
                Etiketler        = dto.Etiketler,
                Kaynak           = dto.Kaynak,
                OlusturmaTarihi  = DateTime.Now,
                GuncellemeTarihi = null,
            };

            _db.MevzuatNotlari.Add(entity);
            await _db.SaveChangesAsync(ct);

            return ToDto(entity);
        }

        public async Task<MevzuatNotuDto?> UpdateAsync(int id, MevzuatNotuDto dto, CancellationToken ct)
        {
            var entity = await _db.MevzuatNotlari.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return null;

            entity.Kategori         = dto.Kategori?.Trim() ?? string.Empty;
            entity.MaddeNo          = dto.MaddeNo;
            entity.Baslik           = dto.Baslik?.Trim() ?? string.Empty;
            entity.Ozet             = dto.Ozet;
            entity.Icerik           = dto.Icerik;
            entity.Etiketler        = dto.Etiketler;
            entity.Kaynak           = dto.Kaynak;
            entity.GuncellemeTarihi = DateTime.Now;

            await _db.SaveChangesAsync(ct);

            return ToDto(entity);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct)
        {
            var entity = await _db.MevzuatNotlari.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return false;

            _db.MevzuatNotlari.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<Dictionary<string, int>> GetKategoriSayilariAsync(CancellationToken ct)
        {
            var gruplar = await _db.MevzuatNotlari
                .AsNoTracking()
                .GroupBy(x => x.Kategori)
                .Select(g => new { Kategori = g.Key, Adet = g.Count() })
                .ToListAsync(ct);

            return gruplar.ToDictionary(g => g.Kategori, g => g.Adet);
        }

        private static MevzuatNotuDto ToDto(MevzuatNotu x) => new()
        {
            Id               = x.Id,
            Kategori         = x.Kategori,
            MaddeNo          = x.MaddeNo,
            Baslik           = x.Baslik,
            Ozet             = x.Ozet,
            Icerik           = x.Icerik,
            Etiketler        = x.Etiketler,
            Kaynak           = x.Kaynak,
            OlusturmaTarihi  = x.OlusturmaTarihi,
            GuncellemeTarihi = x.GuncellemeTarihi,
        };
    }
}
