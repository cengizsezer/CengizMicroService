using CatalogService.Api.Features.Payroll.Entities;
using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Configuration
{
    /// <summary>
    /// Yıla göre anahtarlanan payroll parametre konfigürasyonu.
    /// 2026 değerleri mevcut DB seed'i ile birebir aynı tutuldu (regresyon).
    /// 2024/2025 değerleri kullanıcı tarafından doğrulanan resmî kaynaklara dayanır.
    /// </summary>
    public static class PayrollYearConfigStore
    {
        public static readonly IReadOnlyDictionary<int, PayrollYearConfig> All = BuildAll();

        public static PayrollYearConfig Get(int year)
        {
            if (!All.TryGetValue(year, out var config))
                throw new InvalidOperationException($"'{year}' yılı için payroll konfigürasyonu tanımlı değil.");

            return config;
        }

        public static IReadOnlyList<int> SupportedYears => All.Keys.OrderBy(y => y).ToList();

        private static IReadOnlyDictionary<int, PayrollYearConfig> BuildAll()
        {
            return new Dictionary<int, PayrollYearConfig>
            {
                [2024] = Build2024(),
                [2025] = Build2025(),
                [2026] = Build2026(),
            };
        }

        // ---------- 2024 ----------
        private static PayrollYearConfig Build2024()
        {
            const int year = 2024;
            return new PayrollYearConfig
            {
                Parameter = new PayrollParameter
                {
                    Year = year,
                    IsActive = true,

                    // Çalışan oranları (yıllar arası sabit)
                    SgkEmployeeRate = 0.14m,
                    UnemploymentEmployeeRate = 0.01m,
                    RetiredSgkEmployeeRate = 0.075m,
                    RetiredUnemploymentEmployeeRate = 0m,
                    StampTaxRate = 0.00759m,
                    BesEmployeeRate = 0.03m,

                    // Asgari ücret brütü (2024 doğrulanmış)
                    MinimumWageGrossAmount = 20002.50m,

                    // İstisnalar motorda asgari ücretten türetilir
                    MinimumWageIncomeTaxExemptionMonthly = 0m,
                    MinimumWageStampTaxExemptionMonthly = 0m,
                    MealExemptionDailyTax = 0m,
                    MealExemptionDailySgk = 0m,
                    TransportExemptionDailyTax = 0m,
                    MonthlyFamilyAllowanceExemption = 0m,
                    MonthlyChildAllowanceExemption = 0m,
                    MonthlyBoardMemberExemption = 0m,

                    // İşveren SGK (2024: MYÖ %11, GSS %7.5, KVSK %2)
                    SgkEmployerMYORate = 0.11m,
                    SgkEmployerGSSRate = 0.075m,
                    SgkEmployerKVSKRate = 0.02m,
                    UnemploymentEmployerRate = 0.02m,
                    SgkCeilingMultiplier = 7.5m, // 20002.50 × 7.5 ≈ 150018.75 (resmî tavan 150018.90)

                    // 05510 SGK işveren prim indirimi
                    Incentive05510TreasuryRate = 0.02m,
                    Incentive05510ManufacturingRate = 0.05m,
                    SgkEmployerMYO05510Rate = 0.06m,
                },
                TaxBrackets = new List<PayrollTaxBracket>
                {
                    new() { Year = year, Order = 1, MinAmount = 0m,        MaxAmount = 110000m,   TaxRate = 0.15m },
                    new() { Year = year, Order = 2, MinAmount = 110000m,   MaxAmount = 230000m,   TaxRate = 0.20m },
                    new() { Year = year, Order = 3, MinAmount = 230000m,   MaxAmount = 870000m,   TaxRate = 0.27m },
                    new() { Year = year, Order = 4, MinAmount = 870000m,   MaxAmount = 3000000m,  TaxRate = 0.35m },
                    new() { Year = year, Order = 5, MinAmount = 3000000m,  MaxAmount = null,      TaxRate = 0.40m },
                },
                // Engellilik aylık indirim tutarları (2024 resmî: 6.900 / 4.000 / 1.700)
                DisabilityExemptions = BuildDisabilityExemptions(year, first: 6900m, second: 4000m, third: 1700m),
            };
        }

        // ---------- 2025 ----------
        private static PayrollYearConfig Build2025()
        {
            const int year = 2025;
            return new PayrollYearConfig
            {
                Parameter = new PayrollParameter
                {
                    Year = year,
                    IsActive = true,

                    SgkEmployeeRate = 0.14m,
                    UnemploymentEmployeeRate = 0.01m,
                    RetiredSgkEmployeeRate = 0.075m,
                    RetiredUnemploymentEmployeeRate = 0m,
                    StampTaxRate = 0.00759m,
                    BesEmployeeRate = 0.03m,

                    // Asgari ücret brütü (2025 doğrulanmış)
                    MinimumWageGrossAmount = 26005.50m,

                    MinimumWageIncomeTaxExemptionMonthly = 0m,
                    MinimumWageStampTaxExemptionMonthly = 0m,
                    MealExemptionDailyTax = 0m,
                    MealExemptionDailySgk = 0m,
                    TransportExemptionDailyTax = 0m,
                    MonthlyFamilyAllowanceExemption = 0m,
                    MonthlyChildAllowanceExemption = 0m,
                    MonthlyBoardMemberExemption = 0m,

                    // İşveren SGK (2025: 2024 ile aynı)
                    SgkEmployerMYORate = 0.11m,
                    SgkEmployerGSSRate = 0.075m,
                    SgkEmployerKVSKRate = 0.02m,
                    UnemploymentEmployerRate = 0.02m,
                    SgkCeilingMultiplier = 7.5m, // 26005.50 × 7.5 ≈ 195041.25 (resmî tavan 195041.40)

                    Incentive05510TreasuryRate = 0.02m,
                    Incentive05510ManufacturingRate = 0.05m,
                    SgkEmployerMYO05510Rate = 0.06m,
                },
                TaxBrackets = new List<PayrollTaxBracket>
                {
                    new() { Year = year, Order = 1, MinAmount = 0m,        MaxAmount = 158000m,   TaxRate = 0.15m },
                    new() { Year = year, Order = 2, MinAmount = 158000m,   MaxAmount = 330000m,   TaxRate = 0.20m },
                    new() { Year = year, Order = 3, MinAmount = 330000m,   MaxAmount = 1200000m,  TaxRate = 0.27m },
                    new() { Year = year, Order = 4, MinAmount = 1200000m,  MaxAmount = 4300000m,  TaxRate = 0.35m },
                    new() { Year = year, Order = 5, MinAmount = 4300000m,  MaxAmount = null,      TaxRate = 0.40m },
                },
                // Engellilik aylık indirim tutarları (2025 resmî: 9.900 / 5.700 / 2.400)
                DisabilityExemptions = BuildDisabilityExemptions(year, first: 9900m, second: 5700m, third: 2400m),
            };
        }

        // ---------- 2026 (mevcut seed değerleriyle birebir; regresyon) ----------
        private static PayrollYearConfig Build2026()
        {
            const int year = 2026;
            return new PayrollYearConfig
            {
                Parameter = new PayrollParameter
                {
                    Year = year,
                    IsActive = true,

                    SgkEmployeeRate = 0.14m,
                    UnemploymentEmployeeRate = 0.01m,
                    RetiredSgkEmployeeRate = 0.075m,
                    RetiredUnemploymentEmployeeRate = 0m,
                    StampTaxRate = 0.00759m,
                    BesEmployeeRate = 0.03m,

                    MinimumWageGrossAmount = 33030.00m,

                    MinimumWageIncomeTaxExemptionMonthly = 0m,
                    MinimumWageStampTaxExemptionMonthly = 0m,
                    MealExemptionDailyTax = 0m,
                    MealExemptionDailySgk = 0m,
                    TransportExemptionDailyTax = 0m,
                    MonthlyFamilyAllowanceExemption = 0m,
                    MonthlyChildAllowanceExemption = 0m,
                    MonthlyBoardMemberExemption = 0m,

                    // İşveren SGK — 2026 (7566 sayılı Kanun): MYÖ %11→%12, KVSK %2→%2,25
                    SgkEmployerMYORate = 0.12m,
                    SgkEmployerGSSRate = 0.075m,
                    SgkEmployerKVSKRate = 0.0225m,
                    UnemploymentEmployerRate = 0.02m,
                    SgkCeilingMultiplier = 7.5m,

                    Incentive05510TreasuryRate = 0.02m,
                    Incentive05510ManufacturingRate = 0.05m,
                    SgkEmployerMYO05510Rate = 0.06m,
                },
                TaxBrackets = new List<PayrollTaxBracket>
                {
                    new() { Year = year, Order = 1, MinAmount = 0m,        MaxAmount = 190000m,   TaxRate = 0.15m },
                    new() { Year = year, Order = 2, MinAmount = 190000m,   MaxAmount = 400000m,   TaxRate = 0.20m },
                    new() { Year = year, Order = 3, MinAmount = 400000m,   MaxAmount = 1500000m,  TaxRate = 0.27m },
                    new() { Year = year, Order = 4, MinAmount = 1500000m,  MaxAmount = 5300000m,  TaxRate = 0.35m },
                    new() { Year = year, Order = 5, MinAmount = 5300000m,  MaxAmount = null,      TaxRate = 0.40m },
                },
                DisabilityExemptions = BuildDisabilityExemptions(year, first: 12000m, second: 7000m, third: 3000m),
            };
        }

        private static List<PayrollDisabilityExemption> BuildDisabilityExemptions(
            int year, decimal first, decimal second, decimal third)
        {
            return new List<PayrollDisabilityExemption>
            {
                new() { Year = year, DisabilityType = PayrollDisabilityType.None,        MonthlyExemptionAmount = 0m },
                new() { Year = year, DisabilityType = PayrollDisabilityType.FirstDegree, MonthlyExemptionAmount = first },
                new() { Year = year, DisabilityType = PayrollDisabilityType.SecondDegree, MonthlyExemptionAmount = second },
                new() { Year = year, DisabilityType = PayrollDisabilityType.ThirdDegree, MonthlyExemptionAmount = third },
            };
        }
    }
}
