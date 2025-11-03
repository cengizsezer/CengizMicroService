using CatalogService.Api.Features.Expenses.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    class ReceiptItemEntityTypeConfiguration : IEntityTypeConfiguration<ReceiptItem>
    {
        public void Configure(EntityTypeBuilder<ReceiptItem> builder)
        {
            builder.ToTable("ReceiptItems", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(r => r.Id);

            builder.Property(r => r.ExpenseCode)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(r => r.Type)
                   .IsRequired()
                   .HasMaxLength(50)
                   .HasDefaultValue("Hizmet");

            builder.Property(r => r.AccountingCode)
                   .HasMaxLength(100);

            builder.Property(r => r.AccountingCodeDescription)
                   .HasMaxLength(250);

            builder.Property(r => r.Description)
                   .HasMaxLength(250);

            builder.Property(r => r.Quantity)
                   .HasDefaultValue(1);

            builder.Property(r => r.Unit)
                   .IsRequired()
                   .HasMaxLength(50)
                   .HasDefaultValue("Adet");

            builder.Property(r => r.TotalAmount)
                   .HasColumnType("decimal(18,2)");

            builder.Property(r => r.TotalVat)
                   .HasColumnType("decimal(18,2)");

            builder.Property(r => r.ReceiptNumber)
                   .HasMaxLength(100);

            builder.Property(r => r.ReceiptDate)
                   .IsRequired();

            builder.Property(r => r.ExpenseId)
                   .IsRequired();

            builder.HasMany(r => r.ProductDetails)
                   .WithOne(p => p.ReceiptItem)
                   .HasForeignKey(p => p.ReceiptItemId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(r => r.TenantNo)
              .IsRequired()
              .HasMaxLength(16);

            // Indexler
            builder.HasIndex(r => new { r.TenantNo, r.ExpenseId });
            builder.HasIndex(r => new { r.TenantNo, r.ReceiptDate });
        }
    }
}
