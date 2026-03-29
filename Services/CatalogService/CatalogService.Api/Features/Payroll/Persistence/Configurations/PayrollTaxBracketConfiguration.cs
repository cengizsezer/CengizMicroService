using CatalogService.Api.Features.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Features.Payroll.Persistence.Configurations
{
    public class PayrollTaxBracketConfiguration : IEntityTypeConfiguration<PayrollTaxBracket>
    {
        public void Configure(EntityTypeBuilder<PayrollTaxBracket> builder)
        {
            builder.ToTable("PayrollTaxBrackets", "pkf");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Year)
                .IsRequired();

            builder.Property(x => x.Order)
                .IsRequired();

            builder.Property(x => x.MinAmount)
                .HasPrecision(18, 2);

            builder.Property(x => x.MaxAmount)
                .HasPrecision(18, 2);

            builder.Property(x => x.TaxRate)
                .HasPrecision(18, 4);

            builder.HasIndex(x => new { x.Year, x.Order })
                .IsUnique();
        }
    }
}
