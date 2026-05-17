using CatalogService.Api.Features.KdvBeyanname.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class KdvBeyannameYevmiyeConfiguration : IEntityTypeConfiguration<KdvBeyannameYevmiye>
    {
        public void Configure(EntityTypeBuilder<KdvBeyannameYevmiye> builder)
        {
            builder.ToTable("KdvBeyannameYevmiye");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Donem).IsRequired().HasMaxLength(7);
            builder.Property(x => x.HesapKodu).IsRequired().HasMaxLength(30);
            builder.Property(x => x.HesapAdi).HasMaxLength(200);
            builder.Property(x => x.FisNo).HasMaxLength(50);
            builder.Property(x => x.Aciklama).HasMaxLength(500);
            builder.Property(x => x.FaturaNo).HasMaxLength(80);
            builder.Property(x => x.BelgeTipi).HasMaxLength(50);

            builder.Property(x => x.Borc).HasPrecision(18, 2);
            builder.Property(x => x.Alacak).HasPrecision(18, 2);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.FirmaId, x.Donem });
            builder.HasIndex(x => new { x.FirmaId, x.Donem, x.FaturaNo });
        }
    }
}
