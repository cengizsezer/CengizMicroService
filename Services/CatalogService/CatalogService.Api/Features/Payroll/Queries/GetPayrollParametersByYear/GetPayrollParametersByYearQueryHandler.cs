using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Infrastructure.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollParametersByYear
{
    public class GetPayrollParametersByYearQueryHandler : IRequestHandler<GetPayrollParametersByYearQuery, PayrollParameterDto?>
    {
        private readonly CatalogContext _context;

        public GetPayrollParametersByYearQueryHandler(CatalogContext context)
        {
            _context = context;
        }

        public async Task<PayrollParameterDto?> Handle(GetPayrollParametersByYearQuery request, CancellationToken cancellationToken)
        {
            return await _context.PayrollParameters
                .AsNoTracking()
                .Where(x => x.Year == request.Year && x.IsActive)
                .Select(x => new PayrollParameterDto
                {
                    Year = x.Year,
                    SgkEmployeeRate = x.SgkEmployeeRate,
                    UnemploymentEmployeeRate = x.UnemploymentEmployeeRate,
                    StampTaxRate = x.StampTaxRate,
                    BesEmployeeRate = x.BesEmployeeRate,
                    MinimumWageGrossAmount = x.MinimumWageGrossAmount,
                    MealExemptionDailyTax = x.MealExemptionDailyTax,
                    MealExemptionDailySgk = x.MealExemptionDailySgk,
                    TransportExemptionDailyTax = x.TransportExemptionDailyTax,
                    MonthlyFamilyAllowanceExemption = x.MonthlyFamilyAllowanceExemption,
                    MonthlyChildAllowanceExemption = x.MonthlyChildAllowanceExemption,
                    MonthlyBoardMemberExemption = x.MonthlyBoardMemberExemption,
                    IsActive = x.IsActive
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
