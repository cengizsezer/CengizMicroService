using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public class FirmaKontrolVergiService : IFirmaKontrolVergiService
    {
        private readonly CatalogContext _db;

        public FirmaKontrolVergiService(CatalogContext db) => _db = db;

        public async Task<FirmaKontrolVergiDto?> GetAsync(int firmaId, int donem, int yil, CancellationToken ct = default)
        {
            var e = await _db.FirmaKontrolVergiler
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.FirmaId == firmaId && v.Donem == donem && v.Yil == yil, ct);

            return e is null ? null : ToDto(e);
        }

        public async Task UpsertAsync(int firmaId, FirmaKontrolVergiDto dto, CancellationToken ct = default)
        {
            await EnsureFirmaExistsAsync(firmaId, ct);

            var entity = await _db.FirmaKontrolVergiler
                .FirstOrDefaultAsync(v => v.FirmaId == firmaId && v.Donem == dto.Donem && v.Yil == dto.Yil, ct);

            if (entity is null)
            {
                entity = new FirmaKontrolVergi
                {
                    FirmaId = firmaId,
                    Donem = dto.Donem,
                    Yil = dto.Yil,
                    CreatedAt = DateTime.UtcNow
                };
                _db.FirmaKontrolVergiler.Add(entity);
            }

            // Sadece girdiler yazılır — türetilenler yok.
            entity.Kkeg = dto.Kkeg;
            entity.KkegIstisna = dto.KkegIstisna;
            entity.GecmisYil_2024 = dto.GecmisYil_2024;
            entity.GecmisYil_2023 = dto.GecmisYil_2023;
            entity.GecmisYil_2022 = dto.GecmisYil_2022;
            entity.GecmisYil_2021 = dto.GecmisYil_2021;
            entity.TemettuGeliri = dto.TemettuGeliri;
            entity.BagisYardim = dto.BagisYardim;
            entity.Kv5Indirim = dto.Kv5Indirim;
            entity.GeciciVergi = dto.GeciciVergi;
            entity.BankaStopaji = dto.BankaStopaji;
            entity.DigerTevkifat = dto.DigerTevkifat;
            entity.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        private static FirmaKontrolVergiDto ToDto(FirmaKontrolVergi e) => new()
        {
            Donem = e.Donem,
            Yil = e.Yil,
            Kkeg = e.Kkeg,
            KkegIstisna = e.KkegIstisna,
            GecmisYil_2024 = e.GecmisYil_2024,
            GecmisYil_2023 = e.GecmisYil_2023,
            GecmisYil_2022 = e.GecmisYil_2022,
            GecmisYil_2021 = e.GecmisYil_2021,
            TemettuGeliri = e.TemettuGeliri,
            BagisYardim = e.BagisYardim,
            Kv5Indirim = e.Kv5Indirim,
            GeciciVergi = e.GeciciVergi,
            BankaStopaji = e.BankaStopaji,
            DigerTevkifat = e.DigerTevkifat
        };

        private async Task EnsureFirmaExistsAsync(int firmaId, CancellationToken ct)
        {
            var exists = await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");
        }
    }
}
