using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Strategies
{
    // 4691 sayılı Teknoloji Geliştirme Bölgeleri Kanunu (Teknopark):
    //   Çalışan: gelir vergisi ve damga vergisi tam muafiyet → net maaş artar.
    //   İşveren: SGK payının %50'si Hazine tarafından karşılanır.
    public class Law04691Strategy : IPayrollIncentiveStrategy
    {
        private const decimal EmployerSgkIncentiveRate = 0.5m;

        public void EnrichEmployerCosts(CalculatePayrollResponse response, PayrollCalculationContext context)
        {
            var p = context.Parameter;

            foreach (var month in response.Months)
            {
                // --- Çalışan tarafı: GV ve DV muafiyeti ---
                month.IncomeTaxExemption = month.CalculatedIncomeTax;
                month.PayableIncomeTax = 0m;
                month.StampTaxExemption = month.CalculatedStampTax;
                month.PayableStampTax = 0m;
                month.TotalDeductions = Round2(
                    month.SgkEmployeeAmount +
                    month.UnemploymentEmployeeAmount +
                    month.BesAmount);
                month.NetSalary = Round2(month.GrossSalary - month.TotalDeductions);

                // --- İşveren tarafı: %50 SGK teşviki ---
                var sgkBase = Round2(Math.Min(month.GrossSalary, p.MinimumWageGrossAmount * p.SgkCeilingMultiplier));
                var sgkEmployerGross = Round2(sgkBase * (p.SgkEmployerMYORate + p.SgkEmployerGSSRate + p.SgkEmployerKVSKRate));
                var sgkEmployerIncentive = Round2(sgkEmployerGross * EmployerSgkIncentiveRate);
                var sgkEmployerNet = Round2(sgkEmployerGross - sgkEmployerIncentive);
                var unemploymentEmployerAmount = Round2(sgkBase * p.UnemploymentEmployerRate);

                month.SgkEmployerGross = sgkEmployerGross;
                month.SgkEmployerIncentive = sgkEmployerIncentive;
                month.SgkEmployerNet = sgkEmployerNet;
                month.UnemploymentEmployerAmount = unemploymentEmployerAmount;
                month.TotalEmployerCost = Round2(month.GrossSalary + sgkEmployerNet + unemploymentEmployerAmount);
                month.IncentiveSource = "Hazine";
            }

            if (response.Totals is null) return;

            response.Totals.TotalIncomeTaxExemption = Round2(response.Months.Sum(x => x.IncomeTaxExemption));
            response.Totals.TotalPayableIncomeTax = 0m;
            response.Totals.TotalStampTaxExemption = Round2(response.Months.Sum(x => x.StampTaxExemption));
            response.Totals.TotalPayableStampTax = 0m;
            response.Totals.TotalDeductions = Round2(response.Months.Sum(x => x.TotalDeductions));
            response.Totals.TotalNetSalary = Round2(response.Months.Sum(x => x.NetSalary));

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
