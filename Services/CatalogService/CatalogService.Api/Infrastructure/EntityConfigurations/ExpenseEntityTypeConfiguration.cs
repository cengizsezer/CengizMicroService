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

            builder.Property(e => e.ExpenseCode)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(e => e.PersonnelFullName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(e => e.PersonnelAccountingCode)
                   .HasMaxLength(100);

            builder.Property(e => e.ExpenseDate)
                   .IsRequired();

            builder.Property(e => e.ProjectCode)
                   .HasMaxLength(100);

            builder.Property(e => e.CreatedDate)
                   .IsRequired();

            builder.Property(e => e.CreatedTime)
                   .IsRequired();

            builder.Property(e => e.ApprovedBy)
                   .HasMaxLength(100);

            builder.Property(e => e.ApprovedAt);

            builder.Property(e => e.TotalAmount)
                   .HasColumnType("decimal(18,2)");

            builder.Property(e => e.TotalVat)
                   .HasColumnType("decimal(18,2)");

            builder.Property(e => e.Description)
                   .HasMaxLength(250);

            builder.HasMany(e => e.ReceiptItems)
                   .WithOne(r => r.Expense)
                   .HasForeignKey(r => r.ExpenseId);
        }
    }
}
