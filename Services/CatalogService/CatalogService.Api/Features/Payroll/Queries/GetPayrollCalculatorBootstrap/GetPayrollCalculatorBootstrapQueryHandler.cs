using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Infrastructure.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollCalculatorBootstrap
{
    public class GetPayrollCalculatorBootstrapQueryHandler : IRequestHandler<GetPayrollCalculatorBootstrapQuery, PayrollCalculatorBootstrapDto?>
    {
        private readonly CatalogContext _context;

        public GetPayrollCalculatorBootstrapQueryHandler(CatalogContext context)
        {
            _context = context;
        }

        public async Task<PayrollCalculatorBootstrapDto?> Handle(GetPayrollCalculatorBootstrapQuery request, CancellationToken cancellationToken)
        {
            var parameter = await _context.PayrollParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Year == request.Year && x.IsActive, cancellationToken);

            if (parameter is null)
                return null;

            var dto = new PayrollCalculatorBootstrapDto
            {
                Year = request.Year,
                Parameters = new PayrollParameterDto
                {
                    Year = parameter.Year,
                    SgkEmployeeRate = parameter.SgkEmployeeRate,
                    UnemploymentEmployeeRate = parameter.UnemploymentEmployeeRate,
                    StampTaxRate = parameter.StampTaxRate,
                    BesEmployeeRate = parameter.BesEmployeeRate,
                    MinimumWageGrossAmount = parameter.MinimumWageGrossAmount,
                    MealExemptionDailyTax = parameter.MealExemptionDailyTax,
                    MealExemptionDailySgk = parameter.MealExemptionDailySgk,
                    TransportExemptionDailyTax = parameter.TransportExemptionDailyTax,
                    MonthlyFamilyAllowanceExemption = parameter.MonthlyFamilyAllowanceExemption,
                    MonthlyChildAllowanceExemption = parameter.MonthlyChildAllowanceExemption,
                    MonthlyBoardMemberExemption = parameter.MonthlyBoardMemberExemption,
                    IsActive = parameter.IsActive
                },
                DefaultMonths = Enumerable.Range(1, 12)
                    .Select(m => new PayrollMonthInputDto
                    {
                        Month = m,
                        Amount = 0
                    })
                    .ToList()
            };

            return dto;
        }
    }
}
