using CatalogService.Api.Features.KdvBeyanname.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class DuzenleyenConfiguration : IEntityTypeConfiguration<Duzenleyen>
    {
        public void Configure(EntityTypeBuilder<Duzenleyen> builder)
        {
            builder.ToTable("Duzenleyenler");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Kisaltma).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Vkn).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Soyadi).HasMaxLength(200);
            builder.Property(x => x.Adi).HasMaxLength(200);
            builder.Property(x => x.TicaretSicilNo).HasMaxLength(50);
            builder.Property(x => x.Eposta).HasMaxLength(200);
            builder.Property(x => x.AlanKodu).HasMaxLength(5);
            builder.Property(x => x.TelNo).HasMaxLength(20);

            builder.HasIndex(x => x.Aktif);
        }
    }
}
