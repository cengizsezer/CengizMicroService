using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Mukellefler.Dtos;

namespace CatalogService.Api.Features.Mukellefler.Services
{
    public interface IMukellefService
    {
        Task<PagedResult<MukellefDto>> GetPaginatedAsync(int page = 1, int pageSize = 50, string? search = null, MukellefDurumu? durum = null);
        Task<MukellefDto?> GetByIdAsync(int id);
        Task<MukellefDto> CreateAsync(MukellefCreateDto dto);
        Task<MukellefDto?> UpdateAsync(int id, MukellefUpdateDto dto);
        Task<bool> SoftDeleteAsync(int id);
    }
}
