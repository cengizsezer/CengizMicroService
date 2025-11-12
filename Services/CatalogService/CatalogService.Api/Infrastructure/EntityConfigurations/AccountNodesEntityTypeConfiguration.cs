using CatalogService.Api.Features.AccountPlan;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class AccountNodesEntityTypeConfiguration : IEntityTypeConfiguration<AccountNode>
    {
        public void Configure(EntityTypeBuilder<CatalogService.Api.Features.AccountPlan.AccountNode> builder)
        {
            builder.ToTable("AccountNodes", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(x => x.Name)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(x => x.Description)
                   .HasMaxLength(2000); // İstersen nvarchar(max)

            builder.Property(x => x.Notes)
        .HasColumnType("nvarchar(max)"); // Notlar uzun olabilir

            builder.Property(x => x.Level)
                   .IsRequired();

            builder.Property(x => x.Order)
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Property(x => x.ParentId)
                   .IsRequired(false);

            // Self-referencing ilişki
            builder.HasOne(x => x.Parent)
                   .WithMany(x => x.Children)
                   .HasForeignKey(x => x.ParentId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Indexler
            builder.HasIndex(x => x.Code).IsUnique();                // TDHP'de kod tekil
            builder.HasIndex(x => new { x.ParentId, x.Order });      // ağaç sıralama
            builder.HasIndex(x => new { x.Level, x.Order });         // listelemeler
        }
    }
}

