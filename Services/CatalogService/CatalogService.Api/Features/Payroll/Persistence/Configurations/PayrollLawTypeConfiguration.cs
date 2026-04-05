using CatalogService.Api.Features.Payroll.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Features.Payroll.Persistence.Configurations
{
    public class PayrollLawTypeConfiguration : IEntityTypeConfiguration<PayrollLawType>
    {
        public void Configure(EntityTypeBuilder<PayrollLawType> builder)
        {
            builder.ToTable("PayrollLawTypes", "pkf");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Year)
                .IsRequired();

            builder.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.Property(x => x.DisplayOrder)
                .IsRequired();

            builder.HasIndex(x => new { x.Year, x.Code })
                .IsUnique();
        }
    }
}
