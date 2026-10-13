using CatalogService.Api.Features.Declarations.Dtos;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface IDeclarationQueryService
    {
        Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(int year, int month, int? firmaId = null, string? declarationType = null, bool pasifleriGoster = false);
        Task<YearlyTaxSummaryDto> GetYearlySummaryAsync(int year, int? firmaId = null, bool pasifleriGoster = false);
        Task<List<CompanyYearlySummaryDto>> GetCompanyYearlySummaryAsync(int year, bool pasifleriGoster = false);
    }
}
