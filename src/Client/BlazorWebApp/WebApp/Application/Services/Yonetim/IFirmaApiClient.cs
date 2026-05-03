using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public interface IFirmaApiClient
    {
        Task<List<FirmaDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
        Task<FirmaDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<FirmaDto> CreateAsync(FirmaCreateDto dto, CancellationToken ct = default);
        Task<FirmaDto> UpdateAsync(int id, FirmaUpdateDto dto, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
