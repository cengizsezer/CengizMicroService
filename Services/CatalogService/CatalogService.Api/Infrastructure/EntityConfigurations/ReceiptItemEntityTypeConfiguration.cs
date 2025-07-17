using CatalogService.Api.Core.Domain;
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

            builder.Property(r => r.Item)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(r => r.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(r => r.VatRate)
                .IsRequired()
                .HasColumnType("decimal(5,2)");

            builder.Property(r => r.AccountingCode).HasMaxLength(100);
            builder.Property(r => r.PersonnelCode).HasMaxLength(100);
            builder.Property(r => r.FullName).HasMaxLength(100);
            builder.Property(r => r.Company).HasMaxLength(100);
            builder.Property(r => r.Note).HasMaxLength(250);

            builder.Property(r => r.AmountExclVat)
                   .HasColumnType("decimal(18,2)");

            builder.HasMany(r => r.ProductDetails)
                   .WithOne(p => p.ReceiptItem)
                   .HasForeignKey(p => p.ReceiptItemId);
        }
    }
}
