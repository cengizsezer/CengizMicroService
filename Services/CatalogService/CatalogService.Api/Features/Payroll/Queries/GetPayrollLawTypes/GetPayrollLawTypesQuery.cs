using CatalogService.Api.Features.Payroll.Dtos.Shared;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes
{
    public class GetPayrollLawTypesQuery : IRequest<List<PayrollLawTypeDto>>
    {
        public int Year { get; set; }
    }
}
