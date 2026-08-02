using CatalogService.Api.Features.Muhasebe.Dtos;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// Masraf merkezi tanımları. Hesap planındaki gibi silme yoktur; kullanılmayan
    /// merkez pasife çekilir, geçmiş fişlerde ve raporlarda görünmeye devam eder.
    /// </summary>
    public interface IMasrafMerkeziService
    {
        /// <summary>Fiş girişinin seçim listesi. Varsayılan olarak yalnızca aktif merkezler döner.</summary>
        Task<List<MasrafMerkeziDto>> GetHepsiAsync(bool pasifDahil = false, CancellationToken ct = default);

        Task<MasrafMerkeziDto?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <exception cref="MuhasebeKuralException">Kod veya ad boş/uzun ise.</exception>
        /// <exception cref="Infrastructure.Exceptions.DuplicateRecordException">Kod firmada zaten varsa.</exception>
        Task<MasrafMerkeziDto> CreateAsync(MasrafMerkeziYazDto dto, CancellationToken ct = default);

        /// <summary>Merkezi pasife çeker; pasif merkez yeni fişlerde seçilemez. Yoksa null döner.</summary>
        Task<MasrafMerkeziDto?> PasifeAlAsync(int id, CancellationToken ct = default);
    }
}
