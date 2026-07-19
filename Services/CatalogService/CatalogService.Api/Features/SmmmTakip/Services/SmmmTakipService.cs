using System.Globalization;
using System.Text;
using CatalogService.Api.Features.SmmmTakip.Domain;
using CatalogService.Api.Features.SmmmTakip.Dtos;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.SmmmTakip.Services
{
    public class SmmmTakipService : ISmmmTakipService
    {
        private readonly CatalogContext _db;

        public SmmmTakipService(CatalogContext db) => _db = db;

        // ---- Okuma ----

        public async Task<List<SmmmKonuAgacDto>> GetAgacAsync(CancellationToken ct = default)
        {
            var hepsi = await _db.SmmmKonular
                .AsNoTracking()
                .OrderBy(k => k.Sira).ThenBy(k => k.Baslik)
                .Select(k => new SmmmKonuAgacDto
                {
                    Id = k.Id,
                    UstKonuId = k.UstKonuId,
                    Baslik = k.Baslik,
                    Slug = k.Slug,
                    Sira = k.Sira,
                    Aktif = k.Aktif
                })
                .ToListAsync(ct);

            var byParent = hepsi.ToLookup(k => k.UstKonuId);
            void Ekle(SmmmKonuAgacDto node)
            {
                node.AltKonular = byParent[node.Id].ToList();
                foreach (var alt in node.AltKonular) Ekle(alt);
            }

            var kokler = byParent[null].ToList();
            foreach (var k in kokler) Ekle(k);
            return kokler;
        }

        public Task<SmmmKonuDetayDto?> GetKonuBySlugAsync(string slug, int? yil, CancellationToken ct = default)
            => LoadKonuDetayAsync(k => k.Slug == slug, yil, ct);

        public Task<SmmmKonuDetayDto?> GetKonuByIdAsync(int id, int? yil, CancellationToken ct = default)
            => LoadKonuDetayAsync(k => k.Id == id, yil, ct);

        private async Task<SmmmKonuDetayDto?> LoadKonuDetayAsync(
            System.Linq.Expressions.Expression<Func<SmmmKonu, bool>> predicate, int? yil, CancellationToken ct)
        {
            var konu = await _db.SmmmKonular
                .AsNoTracking()
                .Include(k => k.Hadler).ThenInclude(h => h.Degerler)
                .FirstOrDefaultAsync(predicate, ct);

            if (konu is null) return null;

            return new SmmmKonuDetayDto
            {
                Id = konu.Id,
                UstKonuId = konu.UstKonuId,
                Baslik = konu.Baslik,
                Slug = konu.Slug,
                IcerikMd = konu.IcerikMd,
                Sira = konu.Sira,
                Aktif = konu.Aktif,
                SeciliYil = yil,
                Hadler = konu.Hadler
                    .OrderBy(h => h.Sira).ThenBy(h => h.Ad)
                    .Select(h => ToHadDto(h, yil))
                    .ToList()
            };
        }

        public async Task<List<SmmmHadDto>?> GetHadlerAsync(int konuId, int? yil, CancellationToken ct = default)
        {
            var exists = await _db.SmmmKonular.AnyAsync(k => k.Id == konuId, ct);
            if (!exists) return null;

            var hadler = await _db.SmmmHadler
                .AsNoTracking()
                .Where(h => h.KonuId == konuId)
                .Include(h => h.Degerler)
                .OrderBy(h => h.Sira).ThenBy(h => h.Ad)
                .ToListAsync(ct);

            return hadler.Select(h => ToHadDto(h, yil)).ToList();
        }

        // ---- Konu (admin) ----

        public async Task<SmmmKonuDetayDto> CreateKonuAsync(SmmmKonuSaveDto dto, CancellationToken ct = default)
        {
            var konu = new SmmmKonu
            {
                UstKonuId = dto.UstKonuId,
                Baslik = dto.Baslik.Trim(),
                Slug = await EnsureUniqueSlugAsync(dto.Slug, dto.Baslik, null, ct),
                IcerikMd = dto.IcerikMd,
                Sira = dto.Sira,
                Aktif = dto.Aktif,
                GuncellenmeTarihi = DateTime.UtcNow
            };

            _db.SmmmKonular.Add(konu);
            await _db.SaveChangesAsync(ct);

            return (await GetKonuByIdAsync(konu.Id, null, ct))!;
        }

        public async Task<SmmmKonuDetayDto?> UpdateKonuAsync(int id, SmmmKonuSaveDto dto, CancellationToken ct = default)
        {
            var konu = await _db.SmmmKonular.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (konu is null) return null;

            konu.UstKonuId = dto.UstKonuId;
            konu.Baslik = dto.Baslik.Trim();
            konu.Slug = await EnsureUniqueSlugAsync(dto.Slug, dto.Baslik, id, ct);
            konu.IcerikMd = dto.IcerikMd;
            konu.Sira = dto.Sira;
            konu.Aktif = dto.Aktif;
            konu.GuncellenmeTarihi = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return await GetKonuByIdAsync(id, null, ct);
        }

        public async Task<KonuSilmeSonuc> DeleteKonuAsync(int id, CancellationToken ct = default)
        {
            var konu = await _db.SmmmKonular.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (konu is null) return KonuSilmeSonuc.Bulunamadi;

            // Self-ref FK Restrict: alt konusu varsa silinemez.
            if (await _db.SmmmKonular.AnyAsync(k => k.UstKonuId == id, ct))
                return KonuSilmeSonuc.AltKonuVar;

            _db.SmmmKonular.Remove(konu); // hadler + değerler cascade
            await _db.SaveChangesAsync(ct);
            return KonuSilmeSonuc.Silindi;
        }

        // ---- Had (admin) ----

        public async Task<SmmmHadDto?> CreateHadAsync(SmmmHadSaveDto dto, CancellationToken ct = default)
        {
            var konuVar = await _db.SmmmKonular.AnyAsync(k => k.Id == dto.KonuId, ct);
            if (!konuVar) return null;

            var kod = dto.Kod.Trim();
            if (await _db.SmmmHadler.AnyAsync(h => h.Kod == kod, ct))
                throw new DuplicateRecordException(nameof(dto.Kod), $"'{kod}' kodu zaten kullanılıyor.");

            var had = new SmmmHad
            {
                KonuId = dto.KonuId,
                Kod = dto.Kod.Trim(),
                Ad = dto.Ad.Trim(),
                Aciklama = NullIfEmpty(dto.Aciklama),
                Birim = string.IsNullOrWhiteSpace(dto.Birim) ? "TL" : dto.Birim.Trim(),
                Dayanak = dto.Dayanak?.Trim(),
                Sira = dto.Sira
            };

            _db.SmmmHadler.Add(had);
            await _db.SaveChangesAsync(ct);
            return ToHadDto(had, null);
        }

        public async Task<SmmmHadDto?> UpdateHadAsync(int id, SmmmHadSaveDto dto, CancellationToken ct = default)
        {
            var had = await _db.SmmmHadler.Include(h => h.Degerler).FirstOrDefaultAsync(h => h.Id == id, ct);
            if (had is null) return null;

            var kod = dto.Kod.Trim();
            if (await _db.SmmmHadler.AnyAsync(h => h.Kod == kod && h.Id != id, ct))
                throw new DuplicateRecordException(nameof(dto.Kod), $"'{kod}' kodu zaten kullanılıyor.");

            had.Kod = kod;
            had.Ad = dto.Ad.Trim();
            had.Aciklama = NullIfEmpty(dto.Aciklama);
            had.Birim = string.IsNullOrWhiteSpace(dto.Birim) ? "TL" : dto.Birim.Trim();
            had.Dayanak = dto.Dayanak?.Trim();
            had.Sira = dto.Sira;

            await _db.SaveChangesAsync(ct);
            return ToHadDto(had, null);
        }

        public async Task<bool> DeleteHadAsync(int id, CancellationToken ct = default)
        {
            var had = await _db.SmmmHadler.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (had is null) return false;

            _db.SmmmHadler.Remove(had); // değerler cascade
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Had değeri (admin) — yıl bazlı upsert ----

        public async Task<SmmmHadDegeriDto?> UpsertHadDegeriAsync(SmmmHadDegeriSaveDto dto, CancellationToken ct = default)
        {
            var hadVar = await _db.SmmmHadler.AnyAsync(h => h.Id == dto.HadId, ct);
            if (!hadVar) return null;

            var deger = await _db.SmmmHadDegerleri
                .FirstOrDefaultAsync(d => d.HadId == dto.HadId && d.Yil == dto.Yil, ct);

            if (deger is null)
            {
                deger = new SmmmHadDegeri
                {
                    HadId = dto.HadId,
                    Yil = dto.Yil,
                    Deger = dto.Deger,
                    Teblig = dto.Teblig?.Trim(),
                    Not = dto.Not?.Trim(),
                    GuncellenmeTarihi = DateTime.UtcNow
                };
                _db.SmmmHadDegerleri.Add(deger);
            }
            else
            {
                deger.Deger = dto.Deger;
                deger.Teblig = dto.Teblig?.Trim();
                deger.Not = dto.Not?.Trim();
                deger.GuncellenmeTarihi = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            return ToDegerDto(deger);
        }

        public async Task<bool> DeleteHadDegeriAsync(int id, CancellationToken ct = default)
        {
            var deger = await _db.SmmmHadDegerleri.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (deger is null) return false;

            _db.SmmmHadDegerleri.Remove(deger);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Mapping ----

        private static SmmmHadDto ToHadDto(SmmmHad h, int? yil)
        {
            var secili = yil.HasValue ? h.Degerler.FirstOrDefault(d => d.Yil == yil.Value) : null;
            return new SmmmHadDto
            {
                Id = h.Id,
                KonuId = h.KonuId,
                Kod = h.Kod,
                Ad = h.Ad,
                Aciklama = h.Aciklama,
                Birim = h.Birim,
                Dayanak = h.Dayanak,
                Sira = h.Sira,
                SeciliDeger = secili?.Deger,
                SeciliYil = secili?.Yil,
                SeciliTeblig = secili?.Teblig,
                SeciliNot = secili?.Not,
                Degerler = h.Degerler
                    .OrderByDescending(d => d.Yil)
                    .Select(ToDegerDto)
                    .ToList()
            };
        }

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static SmmmHadDegeriDto ToDegerDto(SmmmHadDegeri d) => new()
        {
            Id = d.Id,
            HadId = d.HadId,
            Yil = d.Yil,
            Deger = d.Deger,
            Teblig = d.Teblig,
            Not = d.Not
        };

        // ---- Slug ----

        private async Task<string> EnsureUniqueSlugAsync(string? requested, string baslik, int? currentId, CancellationToken ct)
        {
            var baseSlug = Slugify(string.IsNullOrWhiteSpace(requested) ? baslik : requested);
            if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "konu";

            var slug = baseSlug;
            var i = 2;
            while (await _db.SmmmKonular.AnyAsync(k => k.Slug == slug && k.Id != currentId, ct))
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
