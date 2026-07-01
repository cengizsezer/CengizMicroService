using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public class FirmaKontrolMizanService : IFirmaKontrolMizanService
    {
        private readonly CatalogContext _db;

        public FirmaKontrolMizanService(CatalogContext db) => _db = db;

        public async Task<List<FirmaKontrolMizanSatirDto>> GetSatirlarAsync(int firmaId, int yil, CancellationToken ct = default)
        {
            return await _db.FirmaKontrolMizanSatirlari
                .AsNoTracking()
                .Where(m => m.FirmaId == firmaId && m.Yil == yil)
                .Select(m => new FirmaKontrolMizanSatirDto
                {
                    Donem = m.Donem,
                    Yil = m.Yil,
                    Kod = m.Kod,
                    Ad = m.Ad,
                    Bakiye = m.Bakiye
                })
                .ToListAsync(ct);
        }

        public async Task KaydetAsync(int firmaId, MizanKaydetRequest req, CancellationToken ct = default)
        {
            await EnsureFirmaExistsAsync(firmaId, ct);

            // Idempotent: aynı (FirmaId, Donem, Yil) için tüm satırları sil, yenilerini yaz.
            var mevcut = await _db.FirmaKontrolMizanSatirlari
                .Where(m => m.FirmaId == firmaId && m.Donem == req.Donem && m.Yil == req.Yil)
                .ToListAsync(ct);

            if (mevcut.Count > 0)
                _db.FirmaKontrolMizanSatirlari.RemoveRange(mevcut);

            // Aynı yükleme içinde tekrar eden kodları teke indir (unique index koruması).
            var eklenenKodlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            foreach (var s in req.Satirlar)
            {
                if (string.IsNullOrWhiteSpace(s.Kod)) continue;
                var kod = s.Kod.Trim();
                if (!eklenenKodlar.Add(kod)) continue;

                _db.FirmaKontrolMizanSatirlari.Add(new FirmaKontrolMizanSatir
                {
                    FirmaId = firmaId,
                    Donem = req.Donem,
                    Yil = req.Yil,
                    Kod = kod,
                    Ad = s.Ad?.Trim() ?? string.Empty,
                    Bakiye = s.Bakiye,
                    UploadedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task SifirlaAsync(int firmaId, int yil, CancellationToken ct = default)
        {
            var mevcut = await _db.FirmaKontrolMizanSatirlari
                .Where(m => m.FirmaId == firmaId && m.Yil == yil)
                .ToListAsync(ct);

            if (mevcut.Count == 0) return;

            _db.FirmaKontrolMizanSatirlari.RemoveRange(mevcut);
            await _db.SaveChangesAsync(ct);
        }

        private async Task EnsureFirmaExistsAsync(int firmaId, CancellationToken ct)
        {
            var exists = await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");
        }
    }
}
