using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public interface IFirmaKontrolMaddeService
    {
        /// <summary>Firmanın DB'de saklı tüm durum satırları (şablon durumları + özel maddeler).</summary>
        Task<List<FirmaKontrolMaddeDto>> GetDurumlarAsync(int firmaId, CancellationToken ct = default);

        /// <summary>Tek maddenin durum/notunu idempotent upsert eder.</summary>
        Task UpsertDurumAsync(int firmaId, FirmaKontrolMaddeUpsertDto dto, CancellationToken ct = default);

        /// <summary>Firmaya özel yeni madde ekler, oluşan satırı döndürür.</summary>
        Task<FirmaKontrolMaddeDto> AddOzelAsync(int firmaId, OzelMaddeCreateDto dto, CancellationToken ct = default);

        /// <summary>Özel maddenin metnini (ve isteğe bağlı kategorisini) günceller. Bulunamazsa null.</summary>
        Task<FirmaKontrolMaddeDto?> UpdateOzelAsync(int firmaId, long id, OzelMaddeUpdateDto dto, CancellationToken ct = default);

        /// <summary>Firmaya özel maddeyi siler. Bulunamazsa false.</summary>
        Task<bool> DeleteOzelAsync(int firmaId, long id, CancellationToken ct = default);
    }
}
