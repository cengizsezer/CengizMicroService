using CatalogService.Api.Features.MevzuatNotlari.Dtos;

namespace CatalogService.Api.Features.MevzuatNotlari.Services
{
    public interface IMevzuatNotuService
    {
        Task<List<MevzuatNotuDto>> GetAllAsync(string? kategori, string? arama, CancellationToken ct);
        Task<MevzuatNotuDto?> GetByIdAsync(int id, CancellationToken ct);
        Task<MevzuatNotuDto> CreateAsync(MevzuatNotuDto dto, CancellationToken ct);
        Task<MevzuatNotuDto?> UpdateAsync(int id, MevzuatNotuDto dto, CancellationToken ct);
        Task<bool> DeleteAsync(int id, CancellationToken ct);
        Task<Dictionary<string, int>> GetKategoriSayilariAsync(CancellationToken ct);
    }
}
