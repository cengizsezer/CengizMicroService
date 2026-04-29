using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Strategies
{
    // 2828 sayılı Sosyal Hizmetler Kanunu — İşveren SGK payının tamamı (MYO+GSS+KVSK) Hazine tarafından karşılanır.
    public class Law02828Strategy : IPayrollIncentiveStrategy
    {
        public void EnrichEmployerCosts(CalculatePayrollResponse response, PayrollCalculationContext context)
        {
            var p = context.Parameter;

            foreach (var month in response.Months)
            {
                var sgkBase = Round2(Math.Min(month.GrossSalary, p.MinimumWageGrossAmount * p.SgkCeilingMultiplier));
                var sgkEmployerGross = Round2(sgkBase * (p.SgkEmployerMYORate + p.SgkEmployerGSSRate + p.SgkEmployerKVSKRate));
                var sgkEmployerNet = 0m;
                var unemploymentEmployerAmount = Round2(sgkBase * p.UnemploymentEmployerRate);

                month.SgkEmployerGross = sgkEmployerGross;
                month.SgkEmployerIncentive = sgkEmployerGross;
                month.SgkEmployerNet = sgkEmployerNet;
                month.UnemploymentEmployerAmount = unemploymentEmployerAmount;
                month.TotalEmployerCost = Round2(month.GrossSalary + sgkEmployerNet + unemploymentEmployerAmount);
                month.IncentiveSource = "Hazine";
            }

            if (response.Totals is null) return;

            response.Totals.TotalSgkEmployerGross = Round2(response.Months.Sum(x => x.SgkEmployerGross ?? 0));
            response.Totals.TotalSgkEmployerIncentive = Round2(response.Months.Sum(x => x.SgkEmployerIncentive ?? 0));
            response.Totals.TotalSgkEmployerNet = Round2(response.Months.Sum(x => x.SgkEmployerNet ?? 0));
            response.Totals.TotalUnemploymentEmployerAmount = Round2(response.Months.Sum(x => x.UnemploymentEmployerAmount ?? 0));
            response.Totals.TotalEmployerCost = Round2(response.Months.Sum(x => x.TotalEmployerCost ?? 0));
        }

        private static decimal Round2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
