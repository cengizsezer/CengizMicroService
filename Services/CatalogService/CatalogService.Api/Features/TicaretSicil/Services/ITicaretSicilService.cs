using CatalogService.Api.Features.TicaretSicil.Dtos;

namespace CatalogService.Api.Features.TicaretSicil.Services
{
    public interface ITicaretSicilService
    {
        // Okuma
        Task<List<TicaretSicilIslemListDto>> GetAllAsync(CancellationToken ct = default);
        Task<TicaretSicilIslemDetayDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
        Task<TicaretSicilIslemDetayDto?> GetByIdAsync(int id, CancellationToken ct = default);

        // İşlem (admin)
        Task<TicaretSicilIslemDetayDto> CreateIslemAsync(TicaretSicilIslemSaveDto dto, CancellationToken ct = default);
        Task<TicaretSicilIslemDetayDto?> UpdateIslemAsync(int id, TicaretSicilIslemSaveDto dto, CancellationToken ct = default);
        /// <summary>Siler ve FileApiService'te temizlenmesi gereken FileId listesini döner; bulunamazsa null.</summary>
        Task<List<int>?> DeleteIslemAsync(int id, CancellationToken ct = default);

        // Adım (admin)
        Task<TicaretSicilAdimDto?> AddAdimAsync(int islemId, TicaretSicilAdimSaveDto dto, CancellationToken ct = default);
        Task<TicaretSicilAdimDto?> UpdateAdimAsync(int adimId, TicaretSicilAdimSaveDto dto, CancellationToken ct = default);
        /// <summary>Siler ve adıma bağlı eklerin FileId listesini döner; bulunamazsa null.</summary>
        Task<List<int>?> DeleteAdimAsync(int adimId, CancellationToken ct = default);
        Task<bool> MoveAdimAsync(int adimId, string direction, CancellationToken ct = default);

        // Ek / belge (admin)
        Task<TicaretSicilEkDto?> AddEkAsync(int adimId, TicaretSicilEkSaveDto dto, CancellationToken ct = default);
        /// <summary>Siler ve silinen ekin FileId'sini döner; bulunamazsa null.</summary>
        Task<int?> DeleteEkAsync(int ekId, CancellationToken ct = default);
    }
}
