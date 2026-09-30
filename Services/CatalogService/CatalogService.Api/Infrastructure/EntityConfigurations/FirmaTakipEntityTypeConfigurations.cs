using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Anasayfa Takip kartının tabloları. Kapsam <c>Firmalar.Id</c>; Firma Bilgileri
    /// tablolarındaki gibi global query filter yok, kapsam her sorguda yazılıyor.
    /// </summary>
    public class FirmaNotuEntityTypeConfiguration : IEntityTypeConfiguration<FirmaNotu>
    {
        public void Configure(EntityTypeBuilder<FirmaNotu> entity)
        {
            entity.ToTable("FirmaNotlari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Metin).IsRequired().HasMaxLength(2000);
            entity.Property(x => x.OlusturanKullaniciId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.OlusturanKullaniciAdi).HasMaxLength(100);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.FirmaId, x.OlusturmaZamani });
        }
    }

    public class FirmaOlayKaydiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaOlayKaydi>
    {
        public void Configure(EntityTypeBuilder<FirmaOlayKaydi> entity)
        {
            entity.ToTable("FirmaOlayKayitlari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.OlayTipi).HasConversion<byte>();
            entity.Property(x => x.Aciklama).IsRequired().HasMaxLength(500);
            entity.Property(x => x.KullaniciId).HasMaxLength(64);
            entity.Property(x => x.KullaniciAdi).HasMaxLength(100);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.FirmaId, x.Zaman });
        }
    }
}
