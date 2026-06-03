using CatalogService.Api.Features.Banka.Dtos;

namespace CatalogService.Api.Features.Banka.Services
{
    public interface IHesapNotService
    {
        Task<List<NotDto>> GetByHesapAsync(int hesapId, int yil, int ay);
        Task<NotDto?> CreateAsync(NotCreateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
