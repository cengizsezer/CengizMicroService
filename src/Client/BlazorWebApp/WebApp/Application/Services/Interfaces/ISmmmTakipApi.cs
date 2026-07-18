using WebApp.Shared.Dto.SmmmTakip;

namespace WebApp.Application.Services.Interfaces
{
    public interface ISmmmTakipApi
    {
        // Okuma
        Task<List<SmmmKonuAgacDto>> GetAgacAsync();
        Task<SmmmKonuDetayDto?> GetKonuBySlugAsync(string slug, int? yil = null);
        Task<SmmmKonuDetayDto?> GetKonuByIdAsync(int id, int? yil = null);
        Task<List<SmmmHadDto>?> GetHadlerAsync(int konuId, int? yil = null);

        // Konu (admin)
        Task<SmmmKonuDetayDto?> CreateKonuAsync(SmmmKonuSaveDto dto);
        Task<SmmmKonuDetayDto?> UpdateKonuAsync(int id, SmmmKonuSaveDto dto);
        Task<bool> DeleteKonuAsync(int id);

        // Had (admin)
        Task<SmmmHadDto?> CreateHadAsync(SmmmHadSaveDto dto);
        Task<SmmmHadDto?> UpdateHadAsync(int id, SmmmHadSaveDto dto);
        Task<bool> DeleteHadAsync(int id);

        // Had değeri (admin) — yıl bazlı upsert
        Task<SmmmHadDegeriDto?> UpsertHadDegeriAsync(SmmmHadDegeriSaveDto dto);
        Task<bool> DeleteHadDegeriAsync(int id);
    }
}
