using CatalogService.Api.Features.Banka.Dtos;

namespace CatalogService.Api.Features.Banka.Services
{
    public interface IBankaTakipService
    {
        Task<List<HesapTakipDto>> GetAyAsync(int year, int month, int? firmaId = null);
        Task<IslemKaydiDto?> IsaretleAsync(IsaretleRequestDto dto);
    }
}
