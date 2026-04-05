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
            await SeedPayrollLawTypesAsync(context, cancellationToken);
        }

        //private static async Task SeedPayrollParametersAsync(CatalogContext context, CancellationToken cancellationToken)
        //{
        //    const int year = 2026;

        //    var exists = await context.PayrollParameters
        //        .AnyAsync(x => x.Year == year, cancellationToken);

        //    if (exists)
        //        return;

        //    var parameter = new PayrollParameter
        //    {
        //        Year = year,

        //        SgkEmployeeRate = 0.14m,
        //        UnemploymentEmployeeRate = 0.01m,
        //        RetiredSgkEmployeeRate = 0.075m,
        //        RetiredUnemploymentEmployeeRate = 0m,
        //        StampTaxRate = 0.00759m,
        //        BesEmployeeRate = 0.03m,

        //        // Yeni doğru yaklaşım: sabit istisna değil, asgari ücret brütü
        //        MinimumWageGrossAmount = 33030.00m,
        //        MinimumWageIncomeTaxExemptionMonthly = 0m,
        //        MinimumWageStampTaxExemptionMonthly = 0m,
        //        MealExemptionDailyTax = 0m,
        //        MealExemptionDailySgk = 0m,
        //        TransportExemptionDailyTax = 0m,

        //        MonthlyFamilyAllowanceExemption = 0m,
        //        MonthlyChildAllowanceExemption = 0m,
        //        MonthlyBoardMemberExemption = 0m,

        //        IsActive = true
        //    };

        //    await context.PayrollParameters.AddAsync(parameter, cancellationToken);
        //    await context.SaveChangesAsync(cancellationToken);
        //}

        private static async Task SeedPayrollParametersAsync(CatalogContext context, CancellationToken cancellationToken)
        {
            const int year = 2026;
            const string seedKey = "PayrollParameters-2026";
            const int seedVersion = 2;

            var appliedSeed = await context.SeedHistories
                .FirstOrDefaultAsync(x => x.SeedKey == seedKey, cancellationToken);

            if (appliedSeed != null && appliedSeed.Version >= seedVersion)
                return;

            var parameter = await context.PayrollParameters
                .FirstOrDefaultAsync(x => x.Year == year, cancellationToken);

            if (parameter == null)
            {
                parameter = new PayrollParameter
                {
                    Year = year
                };

                await context.PayrollParameters.AddAsync(parameter, cancellationToken);
            }

            parameter.SgkEmployeeRate = 0.14m;
            parameter.UnemploymentEmployeeRate = 0.01m;
            parameter.RetiredSgkEmployeeRate = 0.075m;
            parameter.RetiredUnemploymentEmployeeRate = 0m;
            parameter.StampTaxRate = 0.00759m;
            parameter.BesEmployeeRate = 0.03m;
            parameter.MinimumWageGrossAmount = 33030.00m;
            parameter.MinimumWageIncomeTaxExemptionMonthly = 0m;
            parameter.MinimumWageStampTaxExemptionMonthly = 0m;
            parameter.MealExemptionDailyTax = 0m;
            parameter.MealExemptionDailySgk = 0m;
            parameter.TransportExemptionDailyTax = 0m;
            parameter.MonthlyFamilyAllowanceExemption = 0m;
            parameter.MonthlyChildAllowanceExemption = 0m;
            parameter.MonthlyBoardMemberExemption = 0m;
            parameter.IsActive = true;

            if (appliedSeed == null)
            {
                appliedSeed = new SeedHistory
                {
                    SeedKey = seedKey,
                    Version = seedVersion,
                    AppliedAtUtc = DateTime.UtcNow
                };

                await context.SeedHistories.AddAsync(appliedSeed, cancellationToken);
            }
            else
            {
                appliedSeed.Version = seedVersion;
                appliedSeed.AppliedAtUtc = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        //private static async Task SeedPayrollTaxBracketsAsync(CatalogContext context, CancellationToken cancellationToken)
        //{
        //    const int year = 2026;

        //    var exists = await context.PayrollTaxBrackets
        //        .AnyAsync(x => x.Year == year, cancellationToken);

        //    if (exists)
        //        return;

        //    var brackets = new List<PayrollTaxBracket>
        //    {
        //        new()
        //        {
        //            Year = year,
        //            Order = 1,
        //            MinAmount = 0m,
        //            MaxAmount = 190000m,
        //            TaxRate = 0.15m
        //        },
        //        new()
        //        {
        //            Year = year,
        //            Order = 2,
        //            MinAmount = 190000m,
        //            MaxAmount = 400000m,
        //            TaxRate = 0.20m
        //        },
        //        new()
        //        {
        //            Year = year,
        //            Order = 3,
        //            MinAmount = 400000m,
        //            MaxAmount = 1500000m,
        //            TaxRate = 0.27m
        //        },
        //        new()
        //        {
        //            Year = year,
        //            Order = 4,
        //            MinAmount = 1500000m,
        //            MaxAmount = 5300000m,
        //            TaxRate = 0.35m
        //        },
        //        new()
        //        {
        //            Year = year,
        //            Order = 5,
        //            MinAmount = 5300000m,
        //            MaxAmount = null,
        //            TaxRate = 0.40m
        //        }
        //    };

        //    await context.PayrollTaxBrackets.AddRangeAsync(brackets, cancellationToken);
        //    await context.SaveChangesAsync(cancellationToken);
        //}
        private static async Task SeedPayrollTaxBracketsAsync(CatalogContext context, CancellationToken cancellationToken)
        {
            const int year = 2026;
            const string seedKey = "PayrollTaxBrackets-2026";
            const int seedVersion = 1;

            var appliedSeed = await context.SeedHistories
                .FirstOrDefaultAsync(x => x.SeedKey == seedKey, cancellationToken);

            if (appliedSeed != null && appliedSeed.Version >= seedVersion)
                return;

            var existing = await context.PayrollTaxBrackets
                .Where(x => x.Year == year)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
                context.PayrollTaxBrackets.RemoveRange(existing);

            var brackets = new List<PayrollTaxBracket>
    {
        new() { Year = year, Order = 1, MinAmount = 0m, MaxAmount = 190000m, TaxRate = 0.15m },
        new() { Year = year, Order = 2, MinAmount = 190000m, MaxAmount = 400000m, TaxRate = 0.20m },
        new() { Year = year, Order = 3, MinAmount = 400000m, MaxAmount = 1500000m, TaxRate = 0.27m },
        new() { Year = year, Order = 4, MinAmount = 1500000m, MaxAmount = 5300000m, TaxRate = 0.35m },
        new() { Year = year, Order = 5, MinAmount = 5300000m, MaxAmount = null, TaxRate = 0.40m }
    };

            await context.PayrollTaxBrackets.AddRangeAsync(brackets, cancellationToken);

            if (appliedSeed == null)
            {
                await context.SeedHistories.AddAsync(new SeedHistory
                {
                    SeedKey = seedKey,
                    Version = seedVersion,
                    AppliedAtUtc = DateTime.UtcNow
                }, cancellationToken);
            }
            else
            {
                appliedSeed.Version = seedVersion;
                appliedSeed.AppliedAtUtc = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        //private static async Task SeedPayrollDisabilityExemptionsAsync(CatalogContext context, CancellationToken cancellationToken)
        //{
        //    const int year = 2026;

        //    var exists = await context.PayrollDisabilityExemptions
        //        .AnyAsync(x => x.Year == year, cancellationToken);

        //    if (exists)
        //        return;

        //    var disabilityExemptions = new List<PayrollDisabilityExemption>
        //    {
        //        new()
        //        {
        //            Year = year,
        //            DisabilityType = PayrollDisabilityType.None,
        //            MonthlyExemptionAmount = 0m
        //        },
        //        new()
        //        {
        //            Year = year,
        //            DisabilityType = PayrollDisabilityType.FirstDegree,
        //            MonthlyExemptionAmount = 12000m
        //        },
        //        new()
        //        {
        //            Year = year,
        //            DisabilityType = PayrollDisabilityType.SecondDegree,
        //            MonthlyExemptionAmount = 7000m
        //        },
        //        new()
        //        {
        //            Year = year,
        //            DisabilityType = PayrollDisabilityType.ThirdDegree,
        //            MonthlyExemptionAmount = 3000m
        //        }
        //    };

        //    await context.PayrollDisabilityExemptions.AddRangeAsync(disabilityExemptions, cancellationToken);
        //    await context.SaveChangesAsync(cancellationToken);
        //}

        private static async Task SeedPayrollDisabilityExemptionsAsync(CatalogContext context, CancellationToken cancellationToken)
        {
            const int year = 2026;
            const string seedKey = "PayrollDisabilityExemptions-2026";
            const int seedVersion = 1;

            var appliedSeed = await context.SeedHistories
                .FirstOrDefaultAsync(x => x.SeedKey == seedKey, cancellationToken);

            if (appliedSeed != null && appliedSeed.Version >= seedVersion)
                return;

            var existing = await context.PayrollDisabilityExemptions
                .Where(x => x.Year == year)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
                context.PayrollDisabilityExemptions.RemoveRange(existing);

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

            if (appliedSeed == null)
            {
                await context.SeedHistories.AddAsync(new SeedHistory
                {
                    SeedKey = seedKey,
                    Version = seedVersion,
                    AppliedAtUtc = DateTime.UtcNow
                }, cancellationToken);
            }
            else
            {
                appliedSeed.Version = seedVersion;
                appliedSeed.AppliedAtUtc = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }


        private static async Task SeedPayrollLawTypesAsync(CatalogContext context, CancellationToken cancellationToken)
        {
            const int year = 2026;
            const string seedKey = "PayrollLawTypes-2026";
            const int seedVersion = 1;

            var appliedSeed = await context.SeedHistories
                .FirstOrDefaultAsync(x => x.SeedKey == seedKey, cancellationToken);

            if (appliedSeed != null && appliedSeed.Version >= seedVersion)
                return;

            var existingLawTypes = await context.PayrollLawTypes
                .Where(x => x.Year == year)
                .ToListAsync(cancellationToken);

            if (existingLawTypes.Count > 0)
                context.PayrollLawTypes.RemoveRange(existingLawTypes);

            var lawTypes = new List<PayrollLawType>
    {
        new() { Year = year, Code = "00000", Name = "Standart Çalışan", IsActive = true, DisplayOrder = 1 },
        new() { Year = year, Code = "02828", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 2 },
        new() { Year = year, Code = "04691", Name = "Teknopark Personeli", IsActive = true, DisplayOrder = 3 },
        new() { Year = year, Code = "05510", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 4 },
        new() { Year = year, Code = "05746", Name = "Ar-Ge Personeli", IsActive = true, DisplayOrder = 5 },
        new() { Year = year, Code = "06111", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 6 },
        new() { Year = year, Code = "06486", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 7 },
        new() { Year = year, Code = "06645", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 8 },
        new() { Year = year, Code = "14857", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 9 },
        new() { Year = year, Code = "15510", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 10 },
        new() { Year = year, Code = "15746", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 11 },
        new() { Year = year, Code = "16322", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 12 },
        new() { Year = year, Code = "25225", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 13 },
        new() { Year = year, Code = "25510", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 14 },
        new() { Year = year, Code = "26322", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 15 },
        new() { Year = year, Code = "46486", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 16 },
        new() { Year = year, Code = "55225", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 17 },
        new() { Year = year, Code = "56486", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 18 },
        new() { Year = year, Code = "66486", Name = "Sayılı Kanun", IsActive = true, DisplayOrder = 19 }
    };

            await context.PayrollLawTypes.AddRangeAsync(lawTypes, cancellationToken);

            if (appliedSeed == null)
            {
                appliedSeed = new SeedHistory
                {
                    SeedKey = seedKey,
                    Version = seedVersion,
                    AppliedAtUtc = DateTime.UtcNow
                };

                await context.SeedHistories.AddAsync(appliedSeed, cancellationToken);
            }
            else
            {
                appliedSeed.Version = seedVersion;
                appliedSeed.AppliedAtUtc = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
