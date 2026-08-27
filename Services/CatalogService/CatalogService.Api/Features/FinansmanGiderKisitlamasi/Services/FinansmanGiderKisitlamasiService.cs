using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Domain;
using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services
{
    /// <summary>
    /// Hesabın veritabanına bakan tarafı: yılın kısıtlama oranını okur, hesabı motora
    /// yaptırır ve oran tablosunun CRUD'unu yürütür. Tablo ortak referanstır, tenant
    /// filtresi yoktur.
    /// </summary>
    public class FinansmanGiderKisitlamasiService : IFinansmanGiderKisitlamasiService
    {
        private readonly CatalogContext _db;

        public FinansmanGiderKisitlamasiService(CatalogContext db) => _db = db;

        public async Task<FinansmanKisitlamaSonucDto> HesaplaAsync(
            FinansmanKisitlamaHesapRequest request, CancellationToken ct = default)
        {
            var oran = await _db.FinansmanKisitlamaOranlari
                .AsNoTracking()
                .Where(x => x.Yil == request.Yil)
                .Select(x => (decimal?)x.Oran)
                .FirstOrDefaultAsync(ct);

            return FinansmanGiderKisitlamasiMotoru.Hesapla(new FinansmanGiderKisitlamasiMotoru.Girdi
            {
                Yil = request.Yil,
                Ozsermaye = request.Ozsermaye ?? 0m,
                YabanciKaynakToplami = request.YabanciKaynakToplami,
                FinansmanGideri = request.FinansmanGideri,
                OrtuluSermayeVeFinansmanGeliri = request.OrtuluSermayeVeFinansmanGeliri,
                KisitlamaOrani = oran
            });
        }

        public async Task<List<FinansmanKisitlamaOraniDto>> GetOranlarAsync(CancellationToken ct = default)
        {
            var kayitlar = await _db.FinansmanKisitlamaOranlari
                .AsNoTracking()
                .OrderByDescending(x => x.Yil)
                .ToListAsync(ct);

            return kayitlar.Select(Map).ToList();
        }

        public async Task<FinansmanKisitlamaOraniDto?> GetOranAsync(int yil, CancellationToken ct = default)
        {
            var kayit = await _db.FinansmanKisitlamaOranlari
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Yil == yil, ct);

            return kayit is null ? null : Map(kayit);
        }

        public async Task<FinansmanKisitlamaOraniDto> UpsertOranAsync(
            int yil, FinansmanKisitlamaOraniSaveDto dto, CancellationToken ct = default)
        {
            var kayit = await _db.FinansmanKisitlamaOranlari.FirstOrDefaultAsync(x => x.Yil == yil, ct);

            if (kayit is null)
            {
                kayit = new FinansmanKisitlamaOrani { Yil = yil };
                _db.FinansmanKisitlamaOranlari.Add(kayit);
            }

            kayit.Oran = dto.Oran;
            kayit.Dayanak = string.IsNullOrWhiteSpace(dto.Dayanak) ? null : dto.Dayanak.Trim();
            kayit.Not = string.IsNullOrWhiteSpace(dto.Not) ? null : dto.Not.Trim();
            kayit.GuncellenmeTarihi = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            return Map(kayit);
        }

        public async Task<bool> DeleteOranAsync(int yil, CancellationToken ct = default)
        {
            var kayit = await _db.FinansmanKisitlamaOranlari.FirstOrDefaultAsync(x => x.Yil == yil, ct);
            if (kayit is null) return false;

            _db.FinansmanKisitlamaOranlari.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        private static FinansmanKisitlamaOraniDto Map(FinansmanKisitlamaOrani x) => new()
        {
            Id = x.Id,
            Yil = x.Yil,
            Oran = x.Oran,
            Dayanak = x.Dayanak,
            Not = x.Not,
            GuncellenmeTarihi = x.GuncellenmeTarihi
        };
    }
}
