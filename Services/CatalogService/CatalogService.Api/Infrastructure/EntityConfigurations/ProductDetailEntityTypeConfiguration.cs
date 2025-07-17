using CatalogService.Api.Core.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    class ProductDetailEntityTypeConfiguration : IEntityTypeConfiguration<ProductDetail>
    {
        public void Configure(EntityTypeBuilder<ProductDetail> builder)
        {
            builder.ToTable("ProductDetails", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.VatRate)
                .IsRequired()
                .HasColumnType("decimal(5,2)");

            builder.Property(p => p.AccountingCode).HasMaxLength(100);
            builder.Property(p => p.PersonnelCode).HasMaxLength(100);
            builder.Property(p => p.FullName).HasMaxLength(100);
            builder.Property(p => p.Company).HasMaxLength(100);
            builder.Property(p => p.Note).HasMaxLength(250);

            builder.Property(p => p.AmountExclVat)
                   .HasColumnType("decimal(18,2)");
        }
    }
}
