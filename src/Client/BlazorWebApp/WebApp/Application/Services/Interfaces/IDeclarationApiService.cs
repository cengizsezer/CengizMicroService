using CatalogService.Api.Features.Declarations.Dtos;

namespace WebApp.Application.Services.Interfaces
{
    public interface IDeclarationApiService
    {
        Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(
            int year,
            int month,
            int? firmaId = null,
            string? declarationType = null,
            bool pasifleriGoster = false);

        Task<int?> CreateAsync(CreateDeclarationRequest request);
        Task<bool> UpdateAsync(int id, UpdateDeclarationRequest request);
        Task<bool> DeleteAsync(int id);

    }
}
