using CatalogService.Api.Features.KdvBeyanname.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class KdvBeyannameMizanConfiguration : IEntityTypeConfiguration<KdvBeyannameMizan>
    {
        public void Configure(EntityTypeBuilder<KdvBeyannameMizan> builder)
        {
            builder.ToTable("KdvBeyannameMizan");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Donem).IsRequired().HasMaxLength(7);
            builder.Property(x => x.HesapKodu).IsRequired().HasMaxLength(30);
            builder.Property(x => x.HesapAdi).HasMaxLength(200);

            builder.Property(x => x.BorcToplam).HasPrecision(18, 2);
            builder.Property(x => x.AlacakToplam).HasPrecision(18, 2);
            builder.Property(x => x.BorcKalan).HasPrecision(18, 2);
            builder.Property(x => x.AlacakKalan).HasPrecision(18, 2);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.FirmaId, x.Donem, x.HesapKodu }).IsUnique();
        }
    }
}
