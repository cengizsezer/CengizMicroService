using CatalogService.Api.Features.Payroll.Dtos.Shared;

namespace CatalogService.Api.Features.Payroll.Services.Interfaces
{
    public interface IDistributionExportService
    {
        byte[] ExportToExcel(DistributionComparisonResultDto result, decimal stopajRate, int year);
        byte[] ExportToPdf(DistributionComparisonResultDto result, decimal stopajRate, int year);
    }
}
