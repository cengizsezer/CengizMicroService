using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Strategies
{
    // 6486 sayılı Kanun Geçici 57. madde — İşveren SGK payının tamamı ve işveren işsizlik payı
    // İşsizlik Sigortası Fonu tarafından karşılanır.
    // 46486 / 56486 / 66486 kodları aynı teşvikin farklı süre dilimlerini temsil eder; aynı stratejiyi paylaşır.
    public class Law06486Strategy : IPayrollIncentiveStrategy
    {
        public void EnrichEmployerCosts(CalculatePayrollResponse response, PayrollCalculationContext context)
        {
            var p = context.Parameter;

            foreach (var month in response.Months)
            {
                var sgkBase = Round2(Math.Min(month.GrossSalary, p.MinimumWageGrossAmount * p.SgkCeilingMultiplier));
                var sgkEmployerGross = Round2(sgkBase * (p.SgkEmployerMYORate + p.SgkEmployerGSSRate + p.SgkEmployerKVSKRate));

                month.SgkEmployerGross = sgkEmployerGross;
                month.SgkEmployerIncentive = sgkEmployerGross;
                month.SgkEmployerNet = 0m;
                month.UnemploymentEmployerAmount = 0m;
                month.TotalEmployerCost = month.GrossSalary;
                month.IncentiveSource = "İşsizlik Sigortası Fonu";
            }

            if (response.Totals is null) return;

            response.Totals.TotalSgkEmployerGross = Round2(response.Months.Sum(x => x.SgkEmployerGross ?? 0));
            response.Totals.TotalSgkEmployerIncentive = Round2(response.Months.Sum(x => x.SgkEmployerIncentive ?? 0));
            response.Totals.TotalSgkEmployerNet = 0m;
            response.Totals.TotalUnemploymentEmployerAmount = 0m;
            response.Totals.TotalEmployerCost = Round2(response.Months.Sum(x => x.TotalEmployerCost ?? 0));
        }

        private static decimal Round2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
