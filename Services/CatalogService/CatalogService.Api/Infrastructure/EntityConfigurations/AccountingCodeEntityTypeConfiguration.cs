using CatalogService.Api.Core.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class AccountingCodeEntityTypeConfiguration : IEntityTypeConfiguration<AccountingCode>
    {
        public void Configure(EntityTypeBuilder<AccountingCode> builder)
        {
            builder.ToTable("AccountingCodes", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(ac => ac.Id);

            builder.Property(ac => ac.Code)
                   .IsRequired()
                   .HasMaxLength(25);

            builder.Property(ac => ac.Description)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(ac => ac.TenantNo)
               .IsRequired()
               .HasMaxLength(16);

            // Indexler
            builder.HasIndex(ac => new { ac.TenantNo, ac.Code });
        }
    }
}
