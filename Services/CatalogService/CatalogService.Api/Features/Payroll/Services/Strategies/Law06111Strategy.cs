using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Strategies
{
    // 6111 sayılı Kanun Geçici 10. madde — MYO'dan 5 puan Hazine teşviki + işveren işsizlik payı Hazine tarafından karşılanır.
    public class Law06111Strategy : IPayrollIncentiveStrategy
    {
        // 6111 teşvik oranı — 5510 düzeltmesinden etkilenmemesi için bağımsız sabit.
        // Önceden p.Incentive05510TreasuryRate paylaşılıyordu; 5510 2026'da %2'ye
        // düştüğü için bu sabit 6111'in mevcut (%5) davranışını korur.
        // NOT: 6111'in 2026 mevzuatına göre doğru oranı ayrı bir turda gözden geçirilecek.
        private const decimal EmployerSgkIncentiveRate = 0.05m;

        public void EnrichEmployerCosts(CalculatePayrollResponse response, PayrollCalculationContext context)
        {
            var p = context.Parameter;

            foreach (var month in response.Months)
            {
                var sgkBase = Round2(Math.Min(month.GrossSalary, p.MinimumWageGrossAmount * p.SgkCeilingMultiplier));
                var sgkEmployerGross = Round2(sgkBase * (p.SgkEmployerMYORate + p.SgkEmployerGSSRate + p.SgkEmployerKVSKRate));
                var sgkEmployerIncentive = Round2(sgkBase * EmployerSgkIncentiveRate);
                var sgkEmployerNet = Round2(sgkEmployerGross - sgkEmployerIncentive);

                month.SgkEmployerGross = sgkEmployerGross;
                month.SgkEmployerIncentive = sgkEmployerIncentive;
                month.SgkEmployerNet = sgkEmployerNet;
                month.UnemploymentEmployerAmount = 0m;
                month.TotalEmployerCost = Round2(month.GrossSalary + sgkEmployerNet);
                month.IncentiveSource = "Hazine";
            }

            if (response.Totals is null) return;

            response.Totals.TotalSgkEmployerGross = Round2(response.Months.Sum(x => x.SgkEmployerGross ?? 0));
            response.Totals.TotalSgkEmployerIncentive = Round2(response.Months.Sum(x => x.SgkEmployerIncentive ?? 0));
            response.Totals.TotalSgkEmployerNet = Round2(response.Months.Sum(x => x.SgkEmployerNet ?? 0));
            response.Totals.TotalUnemploymentEmployerAmount = 0m;
            response.Totals.TotalEmployerCost = Round2(response.Months.Sum(x => x.TotalEmployerCost ?? 0));
        }

        private static decimal Round2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
