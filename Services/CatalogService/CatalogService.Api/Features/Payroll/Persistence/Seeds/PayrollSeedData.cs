using CatalogService.Api.Features.Payroll.Entities;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Payroll.Persistence.Seeds
{
    public static class PayrollSeedData
    {
        public static async Task SeedAsync(CatalogContext context, CancellationToken cancellationToken = default)
        {
            await SeedPayrollParametersAsync(context, cancellationToken);
            await SeedPayrollTaxBracketsAsync(context, cancellationToken);
            await SeedPayrollDisabilityExemptionsAsync(context, cancellationToken);
        }

        private static async Task SeedPayrollParametersAsync(CatalogContext context, CancellationToken cancellationToken)
        {
            const int year = 2026;

            var exists = await context.PayrollParameters
                .AnyAsync(x => x.Year == year, cancellationToken);

            if (exists)
                return;

            var parameter = new PayrollParameter
            {
                Year = year,

                SgkEmployeeRate = 0.14m,
                UnemploymentEmployeeRate = 0.01m,
                StampTaxRate = 0.00759m,
                BesEmployeeRate = 0.03m,

                // Yeni doğru yaklaşım: sabit istisna değil, asgari ücret brütü
                MinimumWageGrossAmount = 33030.00m,

                MealExemptionDailyTax = 0m,
                MealExemptionDailySgk = 0m,
                TransportExemptionDailyTax = 0m,

                MonthlyFamilyAllowanceExemption = 0m,
                MonthlyChildAllowanceExemption = 0m,
                MonthlyBoardMemberExemption = 0m,

                IsActive = true
            };

            await context.PayrollParameters.AddAsync(parameter, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedPayrollTaxBracketsAsync(CatalogContext context, CancellationToken cancellationToken)
        {
            const int year = 2026;

            var exists = await context.PayrollTaxBrackets
                .AnyAsync(x => x.Year == year, cancellationToken);

            if (exists)
                return;

            var brackets = new List<PayrollTaxBracket>
            {
                new()
                {
                    Year = year,
                    Order = 1,
                    MinAmount = 0m,
                    MaxAmount = 190000m,
                    TaxRate = 0.15m
                },
                new()
                {
                    Year = year,
                    Order = 2,
                    MinAmount = 190000m,
                    MaxAmount = 400000m,
                    TaxRate = 0.20m
                },
                new()
                {
                    Year = year,
                    Order = 3,
                    MinAmount = 400000m,
                    MaxAmount = 1500000m,
                    TaxRate = 0.27m
                },
                new()
                {
                    Year = year,
                    Order = 4,
                    MinAmount = 1500000m,
                    MaxAmount = 5300000m,
                    TaxRate = 0.35m
                },
                new()
                {
                    Year = year,
                    Order = 5,
                    MinAmount = 5300000m,
                    MaxAmount = null,
                    TaxRate = 0.40m
                }
            };

            await context.PayrollTaxBrackets.AddRangeAsync(brackets, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedPayrollDisabilityExemptionsAsync(CatalogContext context, CancellationToken cancellationToken)
        {
            const int year = 2026;

            var exists = await context.PayrollDisabilityExemptions
                .AnyAsync(x => x.Year == year, cancellationToken);

            if (exists)
                return;

            var disabilityExemptions = new List<PayrollDisabilityExemption>
            {
                new()
                {
                    Year = year,
                    DisabilityType = PayrollDisabilityType.None,
                    MonthlyExemptionAmount = 0m
                },
                new()
                {
                    Year = year,
                    DisabilityType = PayrollDisabilityType.FirstDegree,
                    MonthlyExemptionAmount = 12000m
                },
                new()
                {
                    Year = year,
                    DisabilityType = PayrollDisabilityType.SecondDegree,
                    MonthlyExemptionAmount = 7000m
                },
                new()
                {
                    Year = year,
                    DisabilityType = PayrollDisabilityType.ThirdDegree,
                    MonthlyExemptionAmount = 3000m
                }
            };

            await context.PayrollDisabilityExemptions.AddRangeAsync(disabilityExemptions, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
