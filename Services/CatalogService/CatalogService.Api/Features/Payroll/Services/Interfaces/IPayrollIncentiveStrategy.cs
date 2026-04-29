using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Interfaces
{
    public interface IPayrollIncentiveStrategy
    {
        void EnrichEmployerCosts(CalculatePayrollResponse response, PayrollCalculationContext context);
    }
}
