using CatalogService.Api.Features.Payroll.Dtos.Responses;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollCalculatorBootstrap
{
    public class GetPayrollCalculatorBootstrapQuery : IRequest<PayrollCalculatorBootstrapDto?>
    {
        public int Year { get; set; }
    }
}
