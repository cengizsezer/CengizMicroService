using CatalogService.Api.Features.Muhasebe.Dtos;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// Fiş (yevmiye) iş kuralları (10–17). Controller yalnızca doğrulama ve yönlendirme yapar;
    /// tüm kurallar bu katmanda zorlanır.
    /// </summary>
    public interface IFisService
    {
        /// <summary>Tarih aralığı, durum ve hesap filtresiyle fiş listesi.</summary>
        Task<List<FisOzetDto>> GetListeAsync(FisFiltreDto filtre, CancellationToken ct = default);

        /// <summary>Fiş ve satırları. Bulunamazsa null.</summary>
        Task<FisDto?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Yeni fiş (taslak veya kesinleşmiş). Numara firma + dönem bazında üretilir.</summary>
        Task<FisDto> CreateAsync(FisYazDto dto, CancellationToken ct = default);

        /// <summary>Yalnızca taslak fiş güncellenir (iş kuralı 15). Fiş yoksa null.</summary>
        Task<FisDto?> UpdateAsync(int id, FisYazDto dto, CancellationToken ct = default);

        /// <summary>Taslak fişi kesinleştirir; kurallar yeniden doğrulanır. Fiş yoksa null.</summary>
        Task<FisDto?> KesinlestirAsync(int id, CancellationToken ct = default);

        /// <summary>Yalnızca taslak fiş silinir (iş kuralı 15).</summary>
        Task<FisSilmeSonuc> DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Kesinleşmiş fişin borç/alacağını yer değiştirmiş yeni fişini üretir (iş kuralı 15).
        /// Kaynak fiş yoksa null.
        /// </summary>
        Task<FisDto?> TersKayitAsync(int id, TersKayitDto dto, CancellationToken ct = default);
    }
}
