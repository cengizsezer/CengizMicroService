using CatalogService.Api.Features.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Features.Payroll.Persistence.Configurations
{
    public class PayrollDisabilityExemptionConfiguration : IEntityTypeConfiguration<PayrollDisabilityExemption>
    {
        public void Configure(EntityTypeBuilder<PayrollDisabilityExemption> builder)
        {
            builder.ToTable("PayrollDisabilityExemptions","pkf");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Year)
                .IsRequired();

            builder.Property(x => x.DisabilityType)
                .IsRequired();

            builder.Property(x => x.MonthlyExemptionAmount)
                .HasPrecision(18, 2);

            builder.HasIndex(x => new { x.Year, x.DisabilityType })
                .IsUnique();
        }
    }
}
