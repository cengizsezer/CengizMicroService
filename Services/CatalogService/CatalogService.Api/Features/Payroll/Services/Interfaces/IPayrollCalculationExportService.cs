using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;

namespace CatalogService.Api.Features.Payroll.Services.Interfaces
{
    public interface IPayrollCalculationExportService
    {
        byte[] ExportToExcel(CalculatePayrollResponse result, CalculatePayrollRequest request);
        byte[] ExportToPdf(CalculatePayrollResponse result, CalculatePayrollRequest request);
    }
}
