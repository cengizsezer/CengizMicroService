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

            builder.Property(p => p.Rank)
                   .IsRequired();

            builder.Property(p => p.TaxBase)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            builder.Property(p => p.VatRate)
                   .IsRequired()
                   .HasColumnType("decimal(5,2)");

            builder.Property(p => p.VatAmount)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            builder.Property(p => p.TotalAmount)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            builder.Property(p => p.ReceiptItemId)
                   .IsRequired();

            builder.HasOne(p => p.ReceiptItem)
                   .WithMany(r => r.ProductDetails)
                   .HasForeignKey(p => p.ReceiptItemId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(p => p.TenantNo)
             .IsRequired()
             .HasMaxLength(16);

            // Indexler
            builder.HasIndex(p => new { p.TenantNo, p.ReceiptItemId });
        }
    }
}
