using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Entities;

namespace CatalogService.Api.Features.Payroll.Services.Interfaces
{
    public interface IDistributionComparisonService
    {
        DistributionComparisonResultDto Compare(
            int year,
            decimal yillikBrut,
            decimal yillikVergiMaliyeti,
            decimal yillikNet,
            decimal stopajOrani,
            IReadOnlyList<PayrollTaxBracket> taxBrackets);
    }
}
