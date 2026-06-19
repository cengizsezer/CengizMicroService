using System.Globalization;
using System.Text;
using CatalogService.Api.Features.TicaretSicil.Domain;
using CatalogService.Api.Features.TicaretSicil.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.TicaretSicil.Services
{
    public class TicaretSicilService : ITicaretSicilService
    {
        private readonly CatalogContext _db;

        public TicaretSicilService(CatalogContext db) => _db = db;

        // ---- Okuma ----

        public async Task<List<TicaretSicilIslemListDto>> GetAllAsync(CancellationToken ct = default)
        {
            return await _db.TicaretSicilIslemler
                .AsNoTracking()
                .OrderBy(x => x.Sira).ThenBy(x => x.Ad)
                .Select(x => new TicaretSicilIslemListDto
                {
                    Id = x.Id,
                    Slug = x.Slug,
                    Kategori = x.Kategori,
                    Ad = x.Ad,
                    Aciklama = x.Aciklama,
                    Sira = x.Sira,
                    AdimSayisi = x.Adimlar.Count
                })
                .ToListAsync(ct);
        }

        public Task<TicaretSicilIslemDetayDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => LoadDetayAsync(x => x.Slug == slug, ct);

        public Task<TicaretSicilIslemDetayDto?> GetByIdAsync(int id, CancellationToken ct = default)
            => LoadDetayAsync(x => x.Id == id, ct);

        private async Task<TicaretSicilIslemDetayDto?> LoadDetayAsync(
            System.Linq.Expressions.Expression<Func<TicaretSicilIslem, bool>> predicate, CancellationToken ct)
        {
            var islem = await _db.TicaretSicilIslemler
                .AsNoTracking()
                .Include(x => x.Adimlar).ThenInclude(a => a.Ekler)
                .FirstOrDefaultAsync(predicate, ct);

            return islem is null ? null : ToDetay(islem);
        }

        // ---- İşlem (admin) ----

        public async Task<TicaretSicilIslemDetayDto> CreateIslemAsync(TicaretSicilIslemSaveDto dto, CancellationToken ct = default)
        {
            var islem = new TicaretSicilIslem
            {
                Slug = await EnsureUniqueSlugAsync(dto.Slug, dto.Ad, null, ct),
                Kategori = dto.Kategori?.Trim() ?? string.Empty,
                Ad = dto.Ad.Trim(),
                Aciklama = dto.Aciklama?.Trim() ?? string.Empty,
                Sira = dto.Sira,
                CreatedAt = DateTime.UtcNow
            };

            _db.TicaretSicilIslemler.Add(islem);
            await _db.SaveChangesAsync(ct);

            return ToDetay(islem);
        }

        public async Task<TicaretSicilIslemDetayDto?> UpdateIslemAsync(int id, TicaretSicilIslemSaveDto dto, CancellationToken ct = default)
        {
            var islem = await _db.TicaretSicilIslemler
                .Include(x => x.Adimlar).ThenInclude(a => a.Ekler)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (islem is null) return null;

            islem.Slug = await EnsureUniqueSlugAsync(dto.Slug, dto.Ad, id, ct);
            islem.Kategori = dto.Kategori?.Trim() ?? string.Empty;
            islem.Ad = dto.Ad.Trim();
            islem.Aciklama = dto.Aciklama?.Trim() ?? string.Empty;
            islem.Sira = dto.Sira;
            islem.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ToDetay(islem);
        }

        public async Task<List<int>?> DeleteIslemAsync(int id, CancellationToken ct = default)
        {
            var islem = await _db.TicaretSicilIslemler
                .Include(x => x.Adimlar).ThenInclude(a => a.Ekler)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (islem is null) return null;

            var fileIds = islem.Adimlar.SelectMany(a => a.Ekler).Select(e => e.FileId).ToList();

            _db.TicaretSicilIslemler.Remove(islem); // cascade -> adımlar + ekler
            await _db.SaveChangesAsync(ct);
            return fileIds;
        }

        // ---- Adım (admin) ----

        public async Task<TicaretSicilAdimDto?> AddAdimAsync(int islemId, TicaretSicilAdimSaveDto dto, CancellationToken ct = default)
        {
            var exists = await _db.TicaretSicilIslemler.AnyAsync(x => x.Id == islemId, ct);
            if (!exists) return null;

            var nextSira = (await _db.TicaretSicilAdimlar
                .Where(a => a.IslemId == islemId)
                .Select(a => (int?)a.Sira).MaxAsync(ct) ?? 0) + 1;

            var adim = new TicaretSicilAdim
            {
                IslemId = islemId,
                Sira = nextSira,
                Baslik = dto.Baslik.Trim(),
                Aciklama = dto.Aciklama?.Trim(),
                Not = dto.Not?.Trim()
            };

            _db.TicaretSicilAdimlar.Add(adim);
            await _db.SaveChangesAsync(ct);
            return ToAdim(adim);
        }

        public async Task<TicaretSicilAdimDto?> UpdateAdimAsync(int adimId, TicaretSicilAdimSaveDto dto, CancellationToken ct = default)
        {
            var adim = await _db.TicaretSicilAdimlar
                .Include(a => a.Ekler)
                .FirstOrDefaultAsync(a => a.Id == adimId, ct);
            if (adim is null) return null;

            adim.Baslik = dto.Baslik.Trim();
            adim.Aciklama = dto.Aciklama?.Trim();
            adim.Not = dto.Not?.Trim();

            await _db.SaveChangesAsync(ct);
            return ToAdim(adim);
        }

        public async Task<List<int>?> DeleteAdimAsync(int adimId, CancellationToken ct = default)
        {
            var adim = await _db.TicaretSicilAdimlar
                .Include(a => a.Ekler)
                .FirstOrDefaultAsync(a => a.Id == adimId, ct);
            if (adim is null) return null;

            var fileIds = adim.Ekler.Select(e => e.FileId).ToList();

            _db.TicaretSicilAdimlar.Remove(adim); // cascade -> ekler
            await _db.SaveChangesAsync(ct);
            return fileIds;
        }

        public async Task<bool> MoveAdimAsync(int adimId, string direction, CancellationToken ct = default)
        {
            var adim = await _db.TicaretSicilAdimlar.FirstOrDefaultAsync(a => a.Id == adimId, ct);
            if (adim is null) return false;

            var up = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase);

            var siblings = await _db.TicaretSicilAdimlar
                .Where(a => a.IslemId == adim.IslemId)
                .OrderBy(a => a.Sira).ThenBy(a => a.Id)
                .ToListAsync(ct);

            var index = siblings.FindIndex(a => a.Id == adimId);
            var swapIndex = up ? index - 1 : index + 1;
            if (swapIndex < 0 || swapIndex >= siblings.Count) return false;

            var other = siblings[swapIndex];
            (adim.Sira, other.Sira) = (other.Sira, adim.Sira);

            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Ek (admin) ----

        public async Task<TicaretSicilEkDto?> AddEkAsync(int adimId, TicaretSicilEkSaveDto dto, CancellationToken ct = default)
        {
            var exists = await _db.TicaretSicilAdimlar.AnyAsync(a => a.Id == adimId, ct);
            if (!exists) return null;

            var nextSira = (await _db.TicaretSicilEkler
                .Where(e => e.AdimId == adimId)
                .Select(e => (int?)e.Sira).MaxAsync(ct) ?? 0) + 1;

            var ek = new TicaretSicilEk
            {
                AdimId = adimId,
                Ad = dto.Ad.Trim(),
                Tur = dto.Tur,
                DosyaAdi = dto.DosyaAdi,
                ContentType = string.IsNullOrWhiteSpace(dto.ContentType) ? "application/octet-stream" : dto.ContentType,
                FileId = dto.FileId,
                Sira = nextSira,
                CreatedAt = DateTime.UtcNow
            };

            _db.TicaretSicilEkler.Add(ek);
            await _db.SaveChangesAsync(ct);
            return ToEk(ek);
        }

        public async Task<int?> DeleteEkAsync(int ekId, CancellationToken ct = default)
        {
            var ek = await _db.TicaretSicilEkler.FirstOrDefaultAsync(e => e.Id == ekId, ct);
            if (ek is null) return null;

            var fileId = ek.FileId;
            _db.TicaretSicilEkler.Remove(ek);
            await _db.SaveChangesAsync(ct);
            return fileId;
        }

        // ---- Mapping ----

        private static TicaretSicilIslemDetayDto ToDetay(TicaretSicilIslem x) => new()
        {
            Id = x.Id,
            Slug = x.Slug,
            Kategori = x.Kategori,
            Ad = x.Ad,
            Aciklama = x.Aciklama,
            Sira = x.Sira,
            Adimlar = x.Adimlar
                .OrderBy(a => a.Sira).ThenBy(a => a.Id)
                .Select(ToAdim)
                .ToList()
        };

        private static TicaretSicilAdimDto ToAdim(TicaretSicilAdim a) => new()
        {
            Id = a.Id,
            Sira = a.Sira,
            Baslik = a.Baslik,
            Aciklama = a.Aciklama,
            Not = a.Not,
            Ekler = a.Ekler
                .OrderBy(e => e.Sira).ThenBy(e => e.Id)
                .Select(ToEk)
                .ToList()
        };

        private static TicaretSicilEkDto ToEk(TicaretSicilEk e) => new()
        {
            Id = e.Id,
            Ad = e.Ad,
            Tur = e.Tur,
            DosyaAdi = e.DosyaAdi,
            ContentType = e.ContentType,
            FileId = e.FileId
        };

        // ---- Slug ----

        private async Task<string> EnsureUniqueSlugAsync(string? requested, string ad, int? currentId, CancellationToken ct)
        {
            var baseSlug = Slugify(string.IsNullOrWhiteSpace(requested) ? ad : requested);
            if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "islem";

            var slug = baseSlug;
            var i = 2;
            while (await _db.TicaretSicilIslemler.AnyAsync(x => x.Slug == slug && x.Id != currentId, ct))
            {
                slug = $"{baseSlug}-{i}";
                i++;
            }
            return slug;
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            value = value.Trim().ToLowerInvariant();
            value = value
                .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g')
                .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');

            // Aksanları kaldır
            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            value = sb.ToString().Normalize(NormalizationForm.FormC);

            var outSb = new StringBuilder();
            char prevDash = '\0';
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch) && ch < 128)
                {
                    outSb.Append(ch);
                    prevDash = ch;
                }
                else if (prevDash != '-' && outSb.Length > 0)
                {
                    outSb.Append('-');
                    prevDash = '-';
                }
            }

            return outSb.ToString().Trim('-');
        }
    }
}
