using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Features.KdvBeyanname.Dtos;
using CatalogService.Api.Features.KdvBeyanname.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.KdvBeyanname.Services
{
    public interface IKdvUploadService
    {
        Task<MizanUploadResultDto> UploadMizanAsync(
            int firmaId, string donem, Stream excelStream, CancellationToken ct);
        Task<YevmiyeUploadResultDto> UploadYevmiyeAsync(
            int firmaId, string donem, Stream excelStream, CancellationToken ct);
    }

    public class KdvUploadService : IKdvUploadService
    {
        private readonly CatalogContext _db;
        private readonly IKdvMizanExcelParser _mizanParser;
        private readonly IKdvYevmiyeExcelParser _yevmiyeParser;

        public KdvUploadService(
            CatalogContext db,
            IKdvMizanExcelParser mizanParser,
            IKdvYevmiyeExcelParser yevmiyeParser)
        {
            _db = db;
            _mizanParser = mizanParser;
            _yevmiyeParser = yevmiyeParser;
        }

        // ── Mizan ──────────────────────────────────────────────────────────

        public async Task<MizanUploadResultDto> UploadMizanAsync(
            int firmaId, string donem, Stream excelStream, CancellationToken ct)
        {
            ValidateDonem(donem);
            await EnsureFirmaExistsAsync(firmaId, ct);

            var parsed = _mizanParser.Parse(excelStream);

            // Idempotent: aynı (firmaId, donem) için tüm satırları sil, yenilerini yaz.
            await _db.KdvBeyannameMizan
                .Where(m => m.FirmaId == firmaId && m.Donem == donem)
                .ExecuteDeleteAsync(ct);

            var now = DateTime.UtcNow;
            var entities = parsed.Rows.Select(r => new KdvBeyannameMizan
            {
                FirmaId      = firmaId,
                Donem        = donem,
                HesapKodu    = r.HesapKodu,
                HesapAdi     = r.HesapAdi,
                BorcToplam   = r.BorcToplam,
                AlacakToplam = r.AlacakToplam,
                BorcKalan    = r.BorcKalan,
                AlacakKalan  = r.AlacakKalan,
                UploadedAt   = now
            }).ToList();

            await _db.KdvBeyannameMizan.AddRangeAsync(entities, ct);
            await _db.SaveChangesAsync(ct);

            return new MizanUploadResultDto
            {
                OkunanSatir    = parsed.OkunanSatir,
                KaydedilenSatir = entities.Count,
                AtlananSatir   = parsed.AtlananSatir,
                Uyarilar       = parsed.Uyarilar
            };
        }

        // ── Yevmiye ────────────────────────────────────────────────────────

        public async Task<YevmiyeUploadResultDto> UploadYevmiyeAsync(
            int firmaId, string donem, Stream excelStream, CancellationToken ct)
        {
            ValidateDonem(donem);
            await EnsureFirmaExistsAsync(firmaId, ct);

            var parsed = _yevmiyeParser.Parse(excelStream);

            // Idempotent: aynı (firmaId, donem) için tüm satırları sil, yenilerini yaz.
            await _db.KdvBeyannameYevmiye
                .Where(y => y.FirmaId == firmaId && y.Donem == donem)
                .ExecuteDeleteAsync(ct);

            var now = DateTime.UtcNow;
            var entities = parsed.Rows.Select(r => new KdvBeyannameYevmiye
            {
                FirmaId    = firmaId,
                Donem      = donem,
                HesapKodu  = r.HesapKodu,
                HesapAdi   = r.HesapAdi,
                Borc       = r.Borc,
                Alacak     = r.Alacak,
                FisNo      = r.FisNo,
                Tarih      = r.FisTarihi,
                Aciklama   = r.Aciklama,
                FaturaNo   = r.FaturaNo,
                BelgeTipi  = r.BelgeTipi,
                UploadedAt = now
            }).ToList();

            await _db.KdvBeyannameYevmiye.AddRangeAsync(entities, ct);
            await _db.SaveChangesAsync(ct);

            return new YevmiyeUploadResultDto
            {
                OkunanSatir    = parsed.OkunanSatir,
                KaydedilenSatir = entities.Count,
                AtlananSatir   = parsed.AtlananSatir,
                Uyarilar       = parsed.Uyarilar
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private async Task EnsureFirmaExistsAsync(int firmaId, CancellationToken ct)
        {
            var exists = await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");
        }

        private static void ValidateDonem(string donem)
        {
            // YYYY-MM
            if (string.IsNullOrWhiteSpace(donem))
                throw new ArgumentException("Dönem zorunludur (YYYY-MM formatında).");
            if (donem.Length != 7 || donem[4] != '-')
                throw new ArgumentException(
                    $"Dönem formatı geçersiz: '{donem}'. Beklenen: YYYY-MM (örn. 2026-04).");
            if (!int.TryParse(donem.AsSpan(0, 4), out var yil) || yil < 2000 || yil > 2100)
                throw new ArgumentException($"Dönem yılı geçersiz: '{donem}'.");
            if (!int.TryParse(donem.AsSpan(5, 2), out var ay) || ay < 1 || ay > 12)
                throw new ArgumentException($"Dönem ayı geçersiz: '{donem}'.");
        }
    }
}
