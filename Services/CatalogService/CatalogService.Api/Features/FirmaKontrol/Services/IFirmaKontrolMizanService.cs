using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public interface IFirmaKontrolMizanService
    {
        /// <summary>Firmanın verilen yıla ait tüm ham mizan satırları (her iki dönem).</summary>
        Task<List<FirmaKontrolMizanSatirDto>> GetSatirlarAsync(int firmaId, int yil, CancellationToken ct = default);

        /// <summary>Bir dönemin ham mizanını idempotent kaydeder: (FirmaId, Donem, Yil) sil + yaz.</summary>
        Task KaydetAsync(int firmaId, MizanKaydetRequest req, CancellationToken ct = default);

        /// <summary>Firmanın verilen yıla ait tüm mizan satırlarını siler (her iki dönem).</summary>
        Task SifirlaAsync(int firmaId, int yil, CancellationToken ct = default);
    }
}
