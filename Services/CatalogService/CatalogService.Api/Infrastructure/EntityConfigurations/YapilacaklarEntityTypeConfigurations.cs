using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Firma işleri ve Yapılacaklar tabloları. Kapsam <c>Firmalar.Id</c>; Takip kartı
    /// tablolarındaki gibi global query filter yok, kapsam her sorguda yazılıyor.
    /// </summary>
    public class FirmaIsiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaIsi>
    {
        public void Configure(EntityTypeBuilder<FirmaIsi> entity)
        {
            entity.ToTable("FirmaIsleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Baslik).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Aciklama).HasMaxLength(1000);
            entity.Property(x => x.Tekrar).HasConversion<byte>();
            entity.Property(x => x.GunKurali).HasConversion<byte>();
            entity.Property(x => x.TekSeferTarih).HasColumnType("date");
            entity.Property(x => x.SorumluKullaniciAdi).HasMaxLength(100);
            entity.Property(x => x.OlusturanKullaniciId).HasMaxLength(64);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.FirmaId);
        }
    }

    public class VergiTakvimiEntityTypeConfiguration : IEntityTypeConfiguration<VergiTakvimi>
    {
        public void Configure(EntityTypeBuilder<VergiTakvimi> entity)
        {
            entity.ToTable("VergiTakvimi", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.MukellefiyetKodu).IsRequired().HasMaxLength(4);
            entity.Property(x => x.Ad).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Tekrar).HasConversion<byte>();
            entity.Property(x => x.DonemBas).HasColumnType("date");
            entity.Property(x => x.DonemBit).HasColumnType("date");
            entity.Property(x => x.SonGun).HasColumnType("date");

            // Aynı kodun aynı dönemi iki kez girilmesin (iki satır = aynı iş iki kez).
            entity.HasIndex(x => new { x.MukellefiyetKodu, x.Tekrar, x.Yil, x.DonemNo }).IsUnique();
            entity.HasIndex(x => x.SonGun);
        }
    }

    public class IsTamamlamaEntityTypeConfiguration : IEntityTypeConfiguration<IsTamamlama>
    {
        public void Configure(EntityTypeBuilder<IsTamamlama> entity)
        {
            entity.ToTable("IsTamamlamalari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.KaynakTip).HasConversion<byte>();
            entity.Property(x => x.DonemAnahtari).IsRequired().HasMaxLength(16);
            entity.Property(x => x.KullaniciId).HasMaxLength(64);
            entity.Property(x => x.KullaniciAdi).HasMaxLength(100);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.KaynakTip, x.KaynakId, x.FirmaId, x.DonemAnahtari }).IsUnique();
            entity.HasIndex(x => x.FirmaId);
        }
    }
}
