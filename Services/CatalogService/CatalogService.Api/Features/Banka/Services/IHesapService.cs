using CatalogService.Api.Features.Banka.Dtos;

namespace CatalogService.Api.Features.Banka.Services
{
    public interface IHesapService
    {
        Task<List<HesapDto>> GetAllAsync(int? firmaId = null, bool includeInactive = false);
        Task<HesapDto?> GetByIdAsync(int id);
        Task<HesapDto> CreateAsync(HesapCreateDto dto);
        Task<HesapDto?> UpdateAsync(int id, HesapUpdateDto dto);
        Task<bool> SoftDeleteAsync(int id);
    }
}
