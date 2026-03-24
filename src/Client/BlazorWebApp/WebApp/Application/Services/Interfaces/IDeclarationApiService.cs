using CatalogService.Api.Features.Declarations.Dtos;

namespace WebApp.Application.Services.Interfaces
{
    public interface IDeclarationApiService
    {
        Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(
            int year,
            int month,
            int? customerCompanyId = null,
            string? declarationType = null);

        Task<YearlyTaxSummaryDto?> GetYearlySummaryAsync(
            int year,
            int? customerCompanyId = null);

        Task<int?> CreateAsync(CreateDeclarationRequest request);
        Task<bool> UpdateAsync(int id, UpdateDeclarationRequest request);
        Task<bool> DeleteAsync(int id);

    }
}
