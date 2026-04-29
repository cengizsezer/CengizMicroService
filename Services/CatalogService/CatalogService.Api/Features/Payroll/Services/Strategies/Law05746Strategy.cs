using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Strategies
{
    // 5746 sayılı Araştırma, Geliştirme ve Tasarım Faaliyetlerinin Desteklenmesi Kanunu (AR-GE Merkezi):
    //   Çalışan: hesaplanan gelir vergisinin %80'i Hazine'den karşılanır (çalışan %20 öder). Damga vergisi normal.
    //   İşveren: SGK payının %50'si Hazine tarafından karşılanır.
    public class Law05746Strategy : IPayrollIncentiveStrategy
    {
        private const decimal IncomeTaxExemptionRate = 0.80m;
        private const decimal EmployerSgkIncentiveRate = 0.50m;

        public void EnrichEmployerCosts(CalculatePayrollResponse response, PayrollCalculationContext context)
        {
            var p = context.Parameter;

            foreach (var month in response.Months)
            {
                // --- Çalışan tarafı: GV %80 muafiyeti ---
                var incomeTaxExemption = Round2(month.CalculatedIncomeTax * IncomeTaxExemptionRate);
                var payableIncomeTax = Round2(month.CalculatedIncomeTax - incomeTaxExemption);

                month.IncomeTaxExemption = incomeTaxExemption;
                month.PayableIncomeTax = payableIncomeTax;
                month.TotalDeductions = Round2(
                    month.SgkEmployeeAmount +
                    month.UnemploymentEmployeeAmount +
                    payableIncomeTax +
                    month.PayableStampTax +
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
            response.Totals.TotalPayableIncomeTax = Round2(response.Months.Sum(x => x.PayableIncomeTax));
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
