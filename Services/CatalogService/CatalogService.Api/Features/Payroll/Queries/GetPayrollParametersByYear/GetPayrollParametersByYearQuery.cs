using CatalogService.Api.Features.Payroll.Dtos.Shared;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollParametersByYear
{
    public class GetPayrollParametersByYearQuery : IRequest<PayrollParameterDto?>
    {
        public int Year { get; set; }
    }
}
