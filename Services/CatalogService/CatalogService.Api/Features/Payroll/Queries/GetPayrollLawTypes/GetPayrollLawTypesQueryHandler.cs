using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes
{
    public class GetPayrollLawTypesQueryHandler : IRequestHandler<GetPayrollLawTypesQuery, List<PayrollLawTypeDto>>
    {
        public Task<List<PayrollLawTypeDto>> Handle(GetPayrollLawTypesQuery request, CancellationToken cancellationToken)
        {
            var lawTypes = PayrollLawTypeConfigStore.GetForYear(request.Year)
                .Select(x => new PayrollLawTypeDto
                {
                    Code = x.Code,
                    Label = x.Code + " - " + x.Name
                })
                .ToList();

            return Task.FromResult(lawTypes);
        }
    }
}
