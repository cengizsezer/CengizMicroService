using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Infrastructure.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes
{
    public class GetPayrollLawTypesQueryHandler : IRequestHandler<GetPayrollLawTypesQuery, List<PayrollLawTypeDto>>
    {
        private readonly CatalogContext _context;

        public GetPayrollLawTypesQueryHandler(CatalogContext context)
        {
            _context = context;
        }

        public async Task<List<PayrollLawTypeDto>> Handle(GetPayrollLawTypesQuery request, CancellationToken cancellationToken)
        {
            return await _context.PayrollLawTypes
                .AsNoTracking()
                .Where(x => x.Year == request.Year && x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new PayrollLawTypeDto
                {
                    Code = x.Code,
                    Label = x.Code + " - " + x.Name
                })
                .ToListAsync(cancellationToken);
        }
    }
}
