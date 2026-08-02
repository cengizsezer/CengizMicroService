using CatalogService.Api.Features.Muhasebe.Dtos;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// Rapor sorguları (iş kuralları 18–21). Bakiye hiçbir tabloda saklanmaz; her istekte
    /// <c>FisSatir</c> üzerinden hesaplanır. Bu servis yalnızca okuma yapar, hiçbir kayıt değiştirmez.
    /// </summary>
    public interface IRaporService
    {
        /// <summary>
        /// Mizan. <paramref name="seviye"/> verilirse yalnızca o seviyeye kadar olan hesaplar döner
        /// (3 = kebire kadar, 4 = kebir + 1. muavin, boş = tümü).
        /// </summary>
        Task<MizanDto> GetMizanAsync(RaporFiltreDto filtre, byte? seviye, CancellationToken ct = default);

        /// <summary>T cetveli verisi. Hesap bulunamazsa null.</summary>
        Task<EkstreDto?> GetEkstreAsync(int hesapId, RaporFiltreDto filtre, CancellationToken ct = default);

        /// <summary>Masraf merkezi dağılımı ve hesap kırılımı.</summary>
        Task<MasrafMerkeziRaporDto> GetMasrafMerkeziAsync(RaporFiltreDto filtre, CancellationToken ct = default);
    }
}
