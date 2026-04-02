using CatalogService.Api.Features.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Features.Payroll.Persistence.Configurations
{
    public class PayrollParameterConfiguration : IEntityTypeConfiguration<PayrollParameter>
    {
        public void Configure(EntityTypeBuilder<PayrollParameter> builder)
        {
            builder.ToTable("PayrollParameters", "pkf");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Year)
                .IsRequired();

            builder.HasIndex(x => x.Year)
                .IsUnique();

            builder.Property(x => x.SgkEmployeeRate)
                .HasPrecision(18, 4);

            builder.Property(x => x.UnemploymentEmployeeRate)
                .HasPrecision(18, 4);

            builder.Property(x => x.StampTaxRate)
                .HasPrecision(18, 6);

            builder.Property(x => x.BesEmployeeRate)
                .HasPrecision(18, 4);
            builder.Property(x => x.MinimumWageGrossAmount)
                .HasPrecision(18, 2);

            builder.Property(x => x.MealExemptionDailyTax)
                .HasPrecision(18, 2);

            builder.Property(x => x.MealExemptionDailySgk)
                .HasPrecision(18, 2);

            builder.Property(x => x.TransportExemptionDailyTax)
                .HasPrecision(18, 2);

            builder.Property(x => x.MonthlyFamilyAllowanceExemption)
                .HasPrecision(18, 2);

            builder.Property(x => x.MonthlyChildAllowanceExemption)
                .HasPrecision(18, 2);

            builder.Property(x => x.MonthlyBoardMemberExemption)
                .HasPrecision(18, 2);

            builder.Property(x => x.MinimumWageIncomeTaxExemptionMonthly).HasPrecision(18, 2);
            builder.Property(x => x.MinimumWageStampTaxExemptionMonthly).HasPrecision(18, 2);
            builder.Property(x => x.RetiredSgkEmployeeRate).HasPrecision(18, 4);
            builder.Property(x => x.RetiredUnemploymentEmployeeRate).HasPrecision(18, 4);

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);


        }
    }
}
