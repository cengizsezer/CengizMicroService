using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public interface IFinansApiClient
    {
        Task<PagedResult<MukellefFinansDto>> GetPaginatedAsync(
            int page = 1,
            int pageSize = 50,
            string? search = null,
            FinansSinifi? finansSinifi = null,
            CancellationToken ct = default);

        Task<MukellefFinansDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<MukellefFinansDto> UpdateFinansAsync(int id, FinansUpdateDto dto, CancellationToken ct = default);
    }
}
