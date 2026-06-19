using WebApp.Shared.Dto.TicaretSicil;

namespace WebApp.Application.Services.Interfaces
{
    public interface ITicaretSicilApi
    {
        // Okuma
        Task<List<TicaretSicilIslemListDto>> GetAllAsync();
        Task<TicaretSicilIslemDetayDto?> GetBySlugAsync(string slug);
        Task<TicaretSicilIslemDetayDto?> GetByIdAsync(int id);

        // İşlem (admin)
        Task<TicaretSicilIslemDetayDto?> CreateIslemAsync(TicaretSicilIslemSaveDto dto);
        Task<TicaretSicilIslemDetayDto?> UpdateIslemAsync(int id, TicaretSicilIslemSaveDto dto);
        Task<List<int>?> DeleteIslemAsync(int id);

        // Adım (admin)
        Task<TicaretSicilAdimDto?> AddAdimAsync(int islemId, TicaretSicilAdimSaveDto dto);
        Task<TicaretSicilAdimDto?> UpdateAdimAsync(int adimId, TicaretSicilAdimSaveDto dto);
        Task<List<int>?> DeleteAdimAsync(int adimId);
        Task<bool> MoveAdimAsync(int adimId, string direction);

        // Ek (admin)
        Task<TicaretSicilEkDto?> AddEkAsync(int adimId, TicaretSicilEkSaveDto dto);
        Task<int?> DeleteEkAsync(int ekId);
    }
}
