using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public class FirmaKontrolMaddeService : IFirmaKontrolMaddeService
    {
        private readonly CatalogContext _db;

        public FirmaKontrolMaddeService(CatalogContext db) => _db = db;

        public async Task<List<FirmaKontrolMaddeDto>> GetDurumlarAsync(int firmaId, CancellationToken ct = default)
        {
            return await _db.FirmaKontrolMaddeler
                .AsNoTracking()
                .Where(m => m.FirmaId == firmaId)
                .OrderBy(m => m.SiraNo)
                .ThenBy(m => m.Id)
                .Select(m => new FirmaKontrolMaddeDto
                {
                    Id = m.Id,
                    MaddeKey = m.MaddeKey,
                    IsCustom = m.IsCustom,
                    Category = m.Category,
                    SoruMetni = m.SoruMetni,
                    IsChecked = m.IsChecked,
                    Status = m.Status,
                    Not = m.Not,
                    SiraNo = m.SiraNo
                })
                .ToListAsync(ct);
        }

        public async Task UpsertDurumAsync(int firmaId, FirmaKontrolMaddeUpsertDto dto, CancellationToken ct = default)
        {
            await EnsureFirmaExistsAsync(firmaId, ct);

            FirmaKontrolMadde? entity;

            if (dto.IsCustom)
            {
                // Özel madde: Id ile bul. Metnini burada değiştirmiyoruz — sadece durum/not.
                if (dto.Id is null)
                    throw new ArgumentException("Özel madde güncellemesi için Id zorunludur.");

                entity = await _db.FirmaKontrolMaddeler
                    .FirstOrDefaultAsync(m => m.Id == dto.Id.Value && m.FirmaId == firmaId, ct);

                if (entity is null)
                    throw new KeyNotFoundException($"Özel madde bulunamadı: Id={dto.Id}");
            }
            else
            {
                // Şablon maddesi: (FirmaId, MaddeKey) ile bul, yoksa oluştur.
                if (string.IsNullOrWhiteSpace(dto.MaddeKey))
                    throw new ArgumentException("Şablon maddesi güncellemesi için MaddeKey zorunludur.");

                entity = await _db.FirmaKontrolMaddeler
                    .FirstOrDefaultAsync(m => m.FirmaId == firmaId && m.MaddeKey == dto.MaddeKey, ct);

                if (entity is null)
                {
                    entity = new FirmaKontrolMadde
                    {
                        FirmaId = firmaId,
                        MaddeKey = dto.MaddeKey,
                        IsCustom = false,
                        Category = dto.Category,
                        SoruMetni = null,
                        SiraNo = 0,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.FirmaKontrolMaddeler.Add(entity);
                }
            }

            entity.IsChecked = dto.IsChecked;
            entity.Status = dto.Status;
            entity.Not = dto.Not;
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        public async Task<FirmaKontrolMaddeDto> AddOzelAsync(int firmaId, OzelMaddeCreateDto dto, CancellationToken ct = default)
        {
            await EnsureFirmaExistsAsync(firmaId, ct);

            if (string.IsNullOrWhiteSpace(dto.SoruMetni))
                throw new ArgumentException("Özel madde metni boş olamaz.");

            // Yeni özel madde en sona eklensin.
            var maxSira = await _db.FirmaKontrolMaddeler
                .Where(m => m.FirmaId == firmaId && m.IsCustom)
                .Select(m => (int?)m.SiraNo)
                .MaxAsync(ct) ?? 0;

            var entity = new FirmaKontrolMadde
            {
                FirmaId = firmaId,
                MaddeKey = null,
                IsCustom = true,
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "Özel" : dto.Category.Trim(),
                SoruMetni = dto.SoruMetni.Trim(),
                IsChecked = false,
                Status = 0,
                Not = null,
                SiraNo = maxSira + 1,
                CreatedAt = DateTime.UtcNow
            };

            _db.FirmaKontrolMaddeler.Add(entity);
            await _db.SaveChangesAsync(ct);

            return new FirmaKontrolMaddeDto
            {
                Id = entity.Id,
                MaddeKey = entity.MaddeKey,
                IsCustom = entity.IsCustom,
                Category = entity.Category,
                SoruMetni = entity.SoruMetni,
                IsChecked = entity.IsChecked,
                Status = entity.Status,
                Not = entity.Not,
                SiraNo = entity.SiraNo
            };
        }

        public async Task<FirmaKontrolMaddeDto?> UpdateOzelAsync(int firmaId, long id, OzelMaddeUpdateDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.SoruMetni))
                throw new ArgumentException("Özel madde metni boş olamaz.");

            // Yalnızca IsCustom=true maddeler — şablon maddelerine dokunma.
            var entity = await _db.FirmaKontrolMaddeler
                .FirstOrDefaultAsync(m => m.Id == id && m.FirmaId == firmaId && m.IsCustom, ct);

            if (entity is null) return null;

            entity.SoruMetni = dto.SoruMetni.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Category))
                entity.Category = dto.Category.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return new FirmaKontrolMaddeDto
            {
                Id = entity.Id,
                MaddeKey = entity.MaddeKey,
                IsCustom = entity.IsCustom,
                Category = entity.Category,
                SoruMetni = entity.SoruMetni,
                IsChecked = entity.IsChecked,
                Status = entity.Status,
                Not = entity.Not,
                SiraNo = entity.SiraNo
            };
        }

        public async Task<bool> DeleteOzelAsync(int firmaId, long id, CancellationToken ct = default)
        {
            var entity = await _db.FirmaKontrolMaddeler
                .FirstOrDefaultAsync(m => m.Id == id && m.FirmaId == firmaId && m.IsCustom, ct);

            if (entity is null) return false;

            _db.FirmaKontrolMaddeler.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        private async Task EnsureFirmaExistsAsync(int firmaId, CancellationToken ct)
        {
            var exists = await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");
        }
    }
}
