using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollParametersByYear
{
    public class GetPayrollParametersByYearQueryHandler : IRequestHandler<GetPayrollParametersByYearQuery, PayrollParameterDto?>
    {
        public Task<PayrollParameterDto?> Handle(GetPayrollParametersByYearQuery request, CancellationToken cancellationToken)
        {
            if (!PayrollYearConfigStore.All.TryGetValue(request.Year, out var yearConfig))
                return Task.FromResult<PayrollParameterDto?>(null);

            var p = yearConfig.Parameter;
            if (!p.IsActive)
                return Task.FromResult<PayrollParameterDto?>(null);

            var dto = new PayrollParameterDto
            {
                Year = p.Year,
                SgkEmployeeRate = p.SgkEmployeeRate,
                UnemploymentEmployeeRate = p.UnemploymentEmployeeRate,
                StampTaxRate = p.StampTaxRate,
                BesEmployeeRate = p.BesEmployeeRate,
                MinimumWageGrossAmount = p.MinimumWageGrossAmount,
                MealExemptionDailyTax = p.MealExemptionDailyTax,
                MealExemptionDailySgk = p.MealExemptionDailySgk,
                TransportExemptionDailyTax = p.TransportExemptionDailyTax,
                MonthlyFamilyAllowanceExemption = p.MonthlyFamilyAllowanceExemption,
                MonthlyChildAllowanceExemption = p.MonthlyChildAllowanceExemption,
                MonthlyBoardMemberExemption = p.MonthlyBoardMemberExemption,
                IsActive = p.IsActive
            };

            return Task.FromResult<PayrollParameterDto?>(dto);
        }
    }
}
