using CatalogService.Api.Features.TicaretSicil.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class TicaretSicilAdimEntityTypeConfiguration : IEntityTypeConfiguration<TicaretSicilAdim>
    {
        public void Configure(EntityTypeBuilder<TicaretSicilAdim> builder)
        {
            builder.ToTable("TicaretSicilAdimlar", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Baslik)
                   .IsRequired()
                   .HasMaxLength(300);

            builder.Property(x => x.Aciklama)
                   .HasMaxLength(2000);

            builder.Property(x => x.Not)
                   .HasMaxLength(1000);

            builder.HasIndex(x => new { x.IslemId, x.Sira });

            builder.HasMany(x => x.Ekler)
                   .WithOne()
                   .HasForeignKey(e => e.AdimId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
