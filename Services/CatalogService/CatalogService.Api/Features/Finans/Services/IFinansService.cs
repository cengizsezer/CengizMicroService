using CatalogService.Api.Features.Finans.Dtos;
using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Mukellefler.Dtos;

namespace CatalogService.Api.Features.Finans.Services
{
    public interface IFinansService
    {
        Task<PagedResult<MukellefFinansDto>> GetPaginatedAsync(
            int page = 1,
            int pageSize = 50,
            string? search = null,
            FinansSinifi? finansSinifi = null);

        Task<MukellefFinansDto?> GetByIdAsync(int id);

        Task<MukellefFinansDto?> UpdateFinansAsync(int id, FinansUpdateDto dto);
    }
}
