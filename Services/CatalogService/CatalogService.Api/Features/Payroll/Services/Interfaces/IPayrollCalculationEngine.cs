using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Interfaces
{
    public interface IPayrollCalculationEngine
    {
        CalculatePayrollResponse Calculate(
            CalculatePayrollCommand command,
            PayrollCalculationContext context);
    }
}
