using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Features.Expenses.Domain;
using CatalogService.Api.Infrastructure.Context;
using DocumentFormat.OpenXml.Vml.Office;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class DeclarationEntityTypeConfiguration : IEntityTypeConfiguration<Declaration>
    {
        public void Configure(EntityTypeBuilder<Declaration> entity)
        {
            entity.ToTable("Declarations", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TenantNo)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.CompanyName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.DeclarationType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Amount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.HasIndex(x => new { x.TenantNo, x.Year, x.Month });
            entity.HasIndex(x => new { x.Year, x.Month });
        }
    }
}
