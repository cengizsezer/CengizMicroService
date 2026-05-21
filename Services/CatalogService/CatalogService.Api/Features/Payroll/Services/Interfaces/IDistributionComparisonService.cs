using CatalogService.Api.Features.Payroll.Dtos.Shared;

namespace CatalogService.Api.Features.Payroll.Services.Interfaces
{
    public interface IDistributionComparisonService
    {
        DistributionComparisonResultDto Compare(
            int year,
            decimal yillikBrut,
            decimal yillikVergiMaliyeti,
            decimal yillikNet,
            decimal stopajOrani);
    }
}
