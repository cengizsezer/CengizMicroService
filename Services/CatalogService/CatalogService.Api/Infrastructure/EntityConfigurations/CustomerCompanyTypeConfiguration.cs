using CatalogService.Api.Features.Declarations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class CustomerCompanyTypeConfiguration: IEntityTypeConfiguration<CustomerCompany>
    {
        public void Configure(EntityTypeBuilder<CustomerCompany> builder)
        {
            builder.ToTable("CustomerCompanies");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.CompanyName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.TaxNumber)
                .HasMaxLength(20);

            builder.Property(x => x.TaxOffice)
                .HasMaxLength(100);

            builder.Property(x => x.Note)
                .HasMaxLength(500);

            builder.HasIndex(x => new { x.TenantNo, x.CompanyName, x.TaxNumber }).IsUnique();

        }
    }
}
