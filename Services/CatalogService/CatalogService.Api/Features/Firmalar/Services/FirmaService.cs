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

            // DuzenleyenKisaltma için Duzenleyenler ile left-join.
            return await (
                from f in query
                join d in _context.Duzenleyenler.AsNoTracking()
                    on f.DuzenleyenId equals d.Id into dj
                from d in dj.DefaultIfEmpty()
                orderby f.KisaAd
                select new FirmaDto
                {
                    Id                 = f.Id,
                    VergiKimlikNo      = f.VergiKimlikNo,
                    Unvan              = f.Unvan,
                    KisaAd             = f.KisaAd,
                    Email              = f.Email,
                    Telefon            = f.Telefon,
                    TicaretSicilNo     = f.TicaretSicilNo,
                    VergiDairesi       = f.VergiDairesi,
                    VergiDairesiKodu   = f.VergiDairesiKodu,
                    OrkaFirmaKodu      = f.OrkaFirmaKodu,
                    YetkiliAdi         = f.YetkiliAdi,
                    YetkiliSoyadi      = f.YetkiliSoyadi,
                    TelefonAlanKodu    = f.TelefonAlanKodu,
                    DuzenleyenId       = f.DuzenleyenId,
                    DuzenleyenKisaltma = d != null ? d.Kisaltma : null,
                    Aktif              = f.Aktif,
                    CreatedAt          = f.CreatedAt,
                    UpdatedAt          = f.UpdatedAt
                }
            ).ToListAsync();
        }

        public async Task<FirmaDto?> GetByIdAsync(int id)
        {
            var firma = await _context.Firmalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (firma is null) return null;

            string? kisaltma = null;
            if (firma.DuzenleyenId is int dId)
            {
                kisaltma = await _context.Duzenleyenler.AsNoTracking()
                    .Where(d => d.Id == dId)
                    .Select(d => d.Kisaltma)
                    .FirstOrDefaultAsync();
            }
            return ToDto(firma, kisaltma);
        }

        public async Task<FirmaDto> CreateAsync(FirmaCreateDto dto)
        {
            var vkn = dto.VergiKimlikNo.Trim();

            var exists = await _context.Firmalar.AnyAsync(x => x.VergiKimlikNo == vkn);
            if (exists)
                throw new DuplicateRecordException(nameof(Firma.VergiKimlikNo), $"Bu VergiKimlikNo zaten kayıtlı: {vkn}");

            if (dto.DuzenleyenId is int dId &&
                !await _context.Duzenleyenler.AnyAsync(d => d.Id == dId))
            {
                throw new ArgumentException($"Düzenleyen bulunamadı: Id={dId}");
            }

            var firma = new Firma
            {
                VergiKimlikNo = vkn,
                Unvan = dto.Unvan.Trim(),
                KisaAd = dto.KisaAd.Trim(),
                Email = dto.Email.Trim(),
                Telefon = dto.Telefon.Trim(),
                TicaretSicilNo = dto.TicaretSicilNo.Trim(),
                VergiDairesi = dto.VergiDairesi.Trim(),
                VergiDairesiKodu = dto.VergiDairesiKodu?.Trim(),
                OrkaFirmaKodu = dto.OrkaFirmaKodu?.Trim(),
                YetkiliAdi = dto.YetkiliAdi?.Trim(),
                YetkiliSoyadi = dto.YetkiliSoyadi?.Trim(),
                TelefonAlanKodu = dto.TelefonAlanKodu?.Trim(),
                DuzenleyenId = dto.DuzenleyenId,
                Aktif = dto.Aktif,
                CreatedAt = DateTime.UtcNow
            };

            _context.Firmalar.Add(firma);
            await _context.SaveChangesAsync();

            var kisaltma = await ResolveKisaltma(firma.DuzenleyenId);
            return ToDto(firma, kisaltma);
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

            if (dto.DuzenleyenId is int dId &&
                !await _context.Duzenleyenler.AnyAsync(d => d.Id == dId))
            {
                throw new ArgumentException($"Düzenleyen bulunamadı: Id={dId}");
            }

            firma.VergiKimlikNo = vkn;
            firma.Unvan = dto.Unvan.Trim();
            firma.KisaAd = dto.KisaAd.Trim();
            firma.Email = dto.Email.Trim();
            firma.Telefon = dto.Telefon.Trim();
            firma.TicaretSicilNo = dto.TicaretSicilNo.Trim();
            firma.VergiDairesi = dto.VergiDairesi.Trim();
            firma.VergiDairesiKodu = dto.VergiDairesiKodu?.Trim();
            firma.OrkaFirmaKodu = dto.OrkaFirmaKodu?.Trim();
            firma.YetkiliAdi = dto.YetkiliAdi?.Trim();
            firma.YetkiliSoyadi = dto.YetkiliSoyadi?.Trim();
            firma.TelefonAlanKodu = dto.TelefonAlanKodu?.Trim();
            firma.DuzenleyenId = dto.DuzenleyenId;
            firma.Aktif = dto.Aktif;
            firma.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var kisaltma = await ResolveKisaltma(firma.DuzenleyenId);
            return ToDto(firma, kisaltma);
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

        private async Task<string?> ResolveKisaltma(int? duzenleyenId)
        {
            if (duzenleyenId is null) return null;
            return await _context.Duzenleyenler.AsNoTracking()
                .Where(d => d.Id == duzenleyenId.Value)
                .Select(d => d.Kisaltma)
                .FirstOrDefaultAsync();
        }

        private static FirmaDto ToDto(Firma f, string? duzenleyenKisaltma) => new()
        {
            Id = f.Id,
            VergiKimlikNo = f.VergiKimlikNo,
            Unvan = f.Unvan,
            KisaAd = f.KisaAd,
            Email = f.Email,
            Telefon = f.Telefon,
            TicaretSicilNo = f.TicaretSicilNo,
            VergiDairesi = f.VergiDairesi,
            VergiDairesiKodu = f.VergiDairesiKodu,
            OrkaFirmaKodu = f.OrkaFirmaKodu,
            YetkiliAdi = f.YetkiliAdi,
            YetkiliSoyadi = f.YetkiliSoyadi,
            TelefonAlanKodu = f.TelefonAlanKodu,
            DuzenleyenId = f.DuzenleyenId,
            DuzenleyenKisaltma = duzenleyenKisaltma,
            Aktif = f.Aktif,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        };
    }
}
