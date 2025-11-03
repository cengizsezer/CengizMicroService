using CatalogService.Api.Features.Education.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public sealed class EducationItemEntityTypeConfiguration : IEntityTypeConfiguration<EducationItem>
    {
        public void Configure(EntityTypeBuilder<EducationItem> builder)
        {
            // tablo adı + (opsiyonel) şema
            builder.ToTable("EducationItems", schema: "catalog");

            // PK
            builder.HasKey(e => e.Id);

            // indexler
            builder.HasIndex(e => e.Title);              // arama için
            builder.HasIndex(e => e.CreatedAt);          // sıralama/paging için
                                                         // builder.HasIndex(e => new { e.IsPublished, e.CreatedAt });

            // kolon kuralları
            builder.Property(e => e.Title)
                   .IsRequired()
                   .HasMaxLength(300);

            builder.Property(e => e.BodyText)            // NVARCHAR(MAX)
                   .HasColumnType("nvarchar(max)");

            builder.Property(e => e.IsPublished)
                   .HasDefaultValue(true)
                   .IsRequired();

            builder.Property(e => e.CreatedAt)
                   .HasColumnType("datetime2")
                   .HasDefaultValueSql("SYSUTCDATETIME()")
                   .IsRequired();

            builder.Property(e => e.UpdatedAt)
                   .HasColumnType("datetime2")
                   .IsRequired(false);
        }
    }
}
