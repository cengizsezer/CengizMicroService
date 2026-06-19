using CatalogService.Api.Features.TicaretSicil.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class TicaretSicilEkEntityTypeConfiguration : IEntityTypeConfiguration<TicaretSicilEk>
    {
        public void Configure(EntityTypeBuilder<TicaretSicilEk> builder)
        {
            builder.ToTable("TicaretSicilEkler", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Ad)
                   .IsRequired()
                   .HasMaxLength(300);

            builder.Property(x => x.Tur)
                   .HasConversion<int>();

            builder.Property(x => x.DosyaAdi)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(x => x.ContentType)
                   .IsRequired()
                   .HasMaxLength(128)
                   .HasDefaultValue("application/pdf");

            builder.HasIndex(x => x.AdimId);
        }
    }
}
