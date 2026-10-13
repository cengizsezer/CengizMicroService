using CatalogService.Api.Features.FirmaPortfoyu.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>Firma Portföyü aylık mizanı — global; firma silinirse mizanları da gider (Firma Kontrol gibi).</summary>
    public class AylikMizanEntityTypeConfiguration : IEntityTypeConfiguration<AylikMizan>
    {
        public void Configure(EntityTypeBuilder<AylikMizan> entity)
        {
            entity.ToTable("AylikMizanlar", CatalogContext.DEFAULT_SCHEMA, t =>
                t.HasCheckConstraint("CK_AylikMizanlar_Ay", "[Ay] BETWEEN 1 AND 12"));

            entity.HasKey(x => x.Id);
            entity.Property(x => x.DosyaAdi).IsRequired().HasMaxLength(260);
            entity.Property(x => x.YukleyenKullanici).HasMaxLength(100);
            entity.Property(x => x.BorcBakiyeToplami).HasPrecision(18, 2);
            entity.Property(x => x.AlacakBakiyeToplami).HasPrecision(18, 2);
            entity.Property(x => x.DengeFarki).HasPrecision(18, 2);
            entity.Property(x => x.GiderYontemi).HasConversion<byte>();

            // Bir firmanın bir ayı için tek mizan.
            entity.HasIndex(x => new { x.FirmaId, x.Yil, x.Ay }).IsUnique();
            entity.HasIndex(x => new { x.Yil, x.Ay });

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Satirlar).WithOne().HasForeignKey(s => s.AylikMizanId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class AylikMizanSatiriEntityTypeConfiguration : IEntityTypeConfiguration<AylikMizanSatiri>
    {
        public void Configure(EntityTypeBuilder<AylikMizanSatiri> entity)
        {
            entity.ToTable("AylikMizanSatirlari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);
            entity.Property(x => x.HesapKodu).IsRequired().HasMaxLength(3);
            entity.Property(x => x.HesapAdi).HasMaxLength(200);
            entity.Property(x => x.BorcBakiye).HasPrecision(18, 2);
            entity.Property(x => x.AlacakBakiye).HasPrecision(18, 2);
            entity.Property(x => x.Bakiye).HasPrecision(18, 2);

            entity.HasIndex(x => new { x.AylikMizanId, x.HesapKodu }).IsUnique();
        }
    }
}
