using CatalogService.Api.Core.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    class ExpenseEntityTypeConfiguration : IEntityTypeConfiguration<Expense>
    {
        public void Configure(EntityTypeBuilder<Expense> builder)
        {
            builder.ToTable("Expenses", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Company)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.AccountingCode).HasMaxLength(100);
            builder.Property(e => e.PersonnelCode).HasMaxLength(100);
            builder.Property(e => e.FullName).HasMaxLength(100);
            builder.Property(e => e.Note).HasMaxLength(250);

            builder.Property(e => e.AmountExclVat)
                   .HasColumnType("decimal(18,2)");

            builder.Property(e => e.VatRate)
                   .HasColumnType("decimal(5,2)");

            builder.HasMany(e => e.ReceiptDetails)
                   .WithOne(r => r.Expense)
                   .HasForeignKey(r => r.ExpenseId);
        }
    }
}
