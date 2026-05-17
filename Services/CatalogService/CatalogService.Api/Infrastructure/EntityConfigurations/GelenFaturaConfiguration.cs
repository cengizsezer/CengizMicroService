using CatalogService.Api.Features.KdvBeyanname.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class GelenFaturaConfiguration : IEntityTypeConfiguration<GelenFatura>
    {
        public void Configure(EntityTypeBuilder<GelenFatura> builder)
        {
            builder.ToTable("GelenFaturalar");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.FaturaNo).IsRequired().HasMaxLength(50);
            builder.Property(x => x.GondericiVkn).IsRequired().HasMaxLength(20);
            builder.Property(x => x.GondericiUnvan).HasMaxLength(300);
            builder.Property(x => x.ParaBirimi).HasMaxLength(10);
            builder.Property(x => x.Kaynak).HasMaxLength(20);
            builder.Property(x => x.Statu).HasMaxLength(50);

            builder.Property(x => x.MatrahTutari).HasPrecision(18, 2);
            builder.Property(x => x.KdvTutari).HasPrecision(18, 2);
            builder.Property(x => x.ToplamTutar).HasPrecision(18, 2);
            builder.Property(x => x.KdvOrani).HasPrecision(5, 2);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Idempotent upsert için uniq index
            builder.HasIndex(x => new { x.FirmaId, x.FaturaNo, x.GondericiVkn }).IsUnique();
            builder.HasIndex(x => new { x.FirmaId, x.FaturaTarihi });
        }
    }
}
