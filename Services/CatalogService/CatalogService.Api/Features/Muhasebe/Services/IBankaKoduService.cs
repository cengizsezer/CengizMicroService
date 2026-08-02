using CatalogService.Api.Features.Muhasebe.Dtos;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// TCMB EFT katılımcı kodları. Ortak referans veridir (tenant'a bağlı değil),
    /// seed dosyasından okunur ve bellekte tutulur.
    /// </summary>
    public interface IBankaKoduService
    {
        Task<IReadOnlyList<BankaKoduDto>> GetHepsiAsync(CancellationToken ct = default);
    }
}
