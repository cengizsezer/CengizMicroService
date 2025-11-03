using CatalogService.Api.Features.Expenses.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class PersonnelEntityTypeConfiguration : IEntityTypeConfiguration<Personnel>
    {
        public void Configure(EntityTypeBuilder<Personnel> builder)
        {
            builder.ToTable("Personnels", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(e => e.Id);

            builder.Property(e => e.FullName)
                  .IsRequired()
                  .HasMaxLength(100);

            builder.Property(e => e.NormalExpenseNumber)
                 .IsRequired()
                 .HasMaxLength(100);
            builder.Property(e => e.CaseExpenseNumber)
                 .IsRequired()
                 .HasMaxLength(100);
            builder.Property(e => e.SalaryExpenseNumber)
                 .IsRequired()
                 .HasMaxLength(100);

            builder.Property(e => e.NationalId)
                   .IsRequired()
                   .HasMaxLength(11); // TC Kimlik No için ideal

            builder.Property(e => e.FirstName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(e => e.LastName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(e => e.Title)
                   .HasMaxLength(100);

            builder.Property(e => e.PhoneNumber)
                   .HasMaxLength(100);

            builder.Property(e => e.Email)
                   .HasMaxLength(100);

            builder.Property(e => e.IBAN)
                   .HasMaxLength(100);

            builder.Property(e => e.Company)
                   .HasMaxLength(100);

            builder.Property(e => e.Department)
                   .HasMaxLength(100);

            builder.Property(e => e.Unit)
                   .HasMaxLength(100);

            builder.Property(e => e.ExpenseCenter)
                   .HasMaxLength(100);

            builder.Property(e => e.TenantNo)
              .IsRequired()
              .HasMaxLength(16);

            // Indexler
            builder.HasIndex(e => new { e.TenantNo, e.FullName });
            builder.HasIndex(e => new { e.TenantNo, e.NationalId });


        }
    }
}
