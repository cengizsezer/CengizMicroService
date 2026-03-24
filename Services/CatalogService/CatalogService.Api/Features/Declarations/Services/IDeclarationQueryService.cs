using CatalogService.Api.Features.Declarations.Dtos;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface IDeclarationQueryService
    {
        Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(int year, int month, int? customerCompanyId = null, string? declarationType = null);
        Task<YearlyTaxSummaryDto> GetYearlySummaryAsync(int year, int? customerCompanyId = null);
        Task<List<CompanyYearlySummaryDto>> GetCompanyYearlySummaryAsync(int year);
    }
}
