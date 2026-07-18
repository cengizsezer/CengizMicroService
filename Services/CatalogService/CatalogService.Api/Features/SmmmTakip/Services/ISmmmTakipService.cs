using CatalogService.Api.Features.SmmmTakip.Dtos;

namespace CatalogService.Api.Features.SmmmTakip.Services
{
    public enum KonuSilmeSonuc { Silindi, Bulunamadi, AltKonuVar }

    public interface ISmmmTakipService
    {
        // Okuma
        Task<List<SmmmKonuAgacDto>> GetAgacAsync(CancellationToken ct = default);
        Task<SmmmKonuDetayDto?> GetKonuBySlugAsync(string slug, int? yil, CancellationToken ct = default);
        Task<SmmmKonuDetayDto?> GetKonuByIdAsync(int id, int? yil, CancellationToken ct = default);
        Task<List<SmmmHadDto>?> GetHadlerAsync(int konuId, int? yil, CancellationToken ct = default);

        // Konu (admin)
        Task<SmmmKonuDetayDto> CreateKonuAsync(SmmmKonuSaveDto dto, CancellationToken ct = default);
        Task<SmmmKonuDetayDto?> UpdateKonuAsync(int id, SmmmKonuSaveDto dto, CancellationToken ct = default);
        Task<KonuSilmeSonuc> DeleteKonuAsync(int id, CancellationToken ct = default);

        // Had (admin)
        Task<SmmmHadDto?> CreateHadAsync(SmmmHadSaveDto dto, CancellationToken ct = default);
        Task<SmmmHadDto?> UpdateHadAsync(int id, SmmmHadSaveDto dto, CancellationToken ct = default);
        Task<bool> DeleteHadAsync(int id, CancellationToken ct = default);

        // Had değeri (admin) — yıl bazlı upsert
        Task<SmmmHadDegeriDto?> UpsertHadDegeriAsync(SmmmHadDegeriSaveDto dto, CancellationToken ct = default);
        Task<bool> DeleteHadDegeriAsync(int id, CancellationToken ct = default);
    }
}
