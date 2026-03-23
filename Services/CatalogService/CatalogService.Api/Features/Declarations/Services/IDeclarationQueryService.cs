using CatalogService.Api.Features.Declarations.Dtos;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface IDeclarationQueryService
    {
        Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(int year, int month, string? tenantNo = null, string? declarationType = null);
        Task<YearlyTaxSummaryDto> GetYearlySummaryAsync(int year, string? tenantNo = null);
        Task<List<CompanyYearlySummaryDto>> GetCompanyYearlySummaryAsync(int year);
    }
}
