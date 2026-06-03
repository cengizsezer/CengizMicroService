using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public interface IHesapApiClient
    {
        Task<List<HesapDto>> GetAllAsync(int? firmaId = null, bool includeInactive = false, CancellationToken ct = default);
        Task<HesapDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<HesapDto> CreateAsync(HesapCreateDto dto, CancellationToken ct = default);
        Task<HesapDto> UpdateAsync(int id, HesapUpdateDto dto, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
