using WebApp.Shared.Dto.MevzuatNotlari;

namespace WebApp.Application.Services.Interfaces
{
    public interface IMevzuatNotuApi
    {
        Task<List<MevzuatNotuDto>> GetAllAsync(string? kategori = null, string? arama = null);
        Task<Dictionary<string, int>> GetKategoriSayilariAsync();
        Task<MevzuatNotuDto?> GetByIdAsync(int id);
        Task<MevzuatNotuDto?> CreateAsync(MevzuatNotuDto dto);
        Task<MevzuatNotuDto?> UpdateAsync(int id, MevzuatNotuDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
