using CatalogService.Api.Features.Firmalar.Dtos;

namespace CatalogService.Api.Features.Firmalar.Services
{
    public interface IFirmaService
    {
        Task<List<FirmaDto>> GetAllAsync(bool includeInactive = false);
        Task<FirmaDto?> GetByIdAsync(int id);
        Task<FirmaDto> CreateAsync(FirmaCreateDto dto);
        Task<FirmaDto?> UpdateAsync(int id, FirmaUpdateDto dto);
        Task<bool> SoftDeleteAsync(int id);
    }
}
