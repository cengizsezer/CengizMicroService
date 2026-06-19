using CatalogService.Api.Features.TicaretSicil.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class TicaretSicilIslemEntityTypeConfiguration : IEntityTypeConfiguration<TicaretSicilIslem>
    {
        public void Configure(EntityTypeBuilder<TicaretSicilIslem> builder)
        {
            builder.ToTable("TicaretSicilIslemler", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Slug)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(x => x.Kategori)
                   .HasMaxLength(100);

            builder.Property(x => x.Ad)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(x => x.Aciklama)
                   .HasMaxLength(1000);

            builder.HasIndex(x => x.Slug).IsUnique();

            builder.HasMany(x => x.Adimlar)
                   .WithOne()
                   .HasForeignKey(a => a.IslemId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
