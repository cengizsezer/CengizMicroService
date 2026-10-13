using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    /// <summary>
    /// Beyannamelerin firması — Firmalarım (dbo.Firmalar). Eski dbo.CustomerCompanies artık OKUNMAZ (tablo, seed ve
    /// CustomerCompaniesController duruyor). Firmalar global (tenant'sız), Firmalarım'la aynı kapsam; seçicide yalnız
    /// Aktif firmalar, pasif firmanın kayıtları istenirse ("Pasif firmaları da göster") görünür.
    /// </summary>
    public sealed record BeyannameFirmasi(int Id, string Ad, string? VergiKimlikNo, bool Aktif)
    {
        /// <summary>FirmaId'si olmayan (VKN ile eşleşmemiş) eski kayıtların grubu.</summary>
        public const string EslesmeyenAdi = "Eşleşmeyen firma";

        /// <summary>Görüntü adı: kısa ad, yoksa unvan (Firmalarım'daki seçicilerle aynı).</summary>
        public static string AdOf(string? kisaAd, string? unvan)
            => string.IsNullOrWhiteSpace(kisaAd) ? (unvan ?? string.Empty).Trim() : kisaAd.Trim();

        /// <summary>Firmalarım'daki firmalar (pasifler dahil — pasif kayıtların adı ve durumu için).</summary>
        public static async Task<List<BeyannameFirmasi>> HepsiAsync(CatalogContext db, CancellationToken ct = default)
            => (await db.Firmalar.AsNoTracking()
                    .Select(f => new { f.Id, f.KisaAd, f.Unvan, f.VergiKimlikNo, f.Aktif })
                    .ToListAsync(ct))
                .Select(f => new BeyannameFirmasi(f.Id, AdOf(f.KisaAd, f.Unvan), f.VergiKimlikNo, f.Aktif))
                .ToList();
    }
}
