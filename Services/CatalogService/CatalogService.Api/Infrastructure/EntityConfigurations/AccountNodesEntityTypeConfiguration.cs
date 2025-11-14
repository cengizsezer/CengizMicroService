using CatalogService.Api.Features.AccountPlan;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class AccountNodesEntityTypeConfiguration : IEntityTypeConfiguration<AccountNode>
    {
        public void Configure(EntityTypeBuilder<AccountNode> builder)
        {
            builder.ToTable("AccountNodes", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            // 🔴 BUNU EKLE
            builder.Property(x => x.Id)
                   .ValueGeneratedNever();  // Id'yi EF tarafından üretilmeyecek yap

            builder.Property(x => x.Code)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(x => x.Name)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(x => x.Description)
                   .HasMaxLength(2000);

            builder.Property(x => x.Notes)
                   .HasColumnType("nvarchar(max)");

            builder.Property(x => x.Level)
                   .IsRequired();

            builder.Property(x => x.Order)
                   .IsRequired()
                   .HasDefaultValue(0);

            builder.Property(x => x.ParentId)
                   .IsRequired(false);

            builder.HasOne(x => x.Parent)
                   .WithMany(x => x.Children)
                   .HasForeignKey(x => x.ParentId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.Code).IsUnique();
            builder.HasIndex(x => new { x.ParentId, x.Order });
            builder.HasIndex(x => new { x.Level, x.Order });
        }

    }
}

