using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Parolalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Yönetim › Parolalar — tek tablo, Tur alanı. Firma FK'si nullable ve SetNull: firma silinmesini engellemez; kayıt
    /// silinmez, FirmaAdi'ndaki kopya adla kalır. Şifreli sütunlar nvarchar(max) (Data Protection çıktısı uzun).
    /// </summary>
    public class ParolaEntityTypeConfiguration : IEntityTypeConfiguration<Parola>
    {
        public void Configure(EntityTypeBuilder<Parola> entity)
        {
            entity.ToTable("Parolalar", CatalogContext.DEFAULT_SCHEMA, t =>
                t.HasCheckConstraint("CK_Parolalar_Tur", "[Tur] BETWEEN 1 AND 3"));

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Tur).HasConversion<byte>();
            entity.Property(x => x.FirmaAdi).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Entegrator).HasMaxLength(100);
            entity.Property(x => x.PortalAdresi).HasMaxLength(500);
            entity.Property(x => x.KullaniciAdi).IsRequired().HasMaxLength(100);
            entity.Property(x => x.KullaniciKoduEk).HasMaxLength(20);
            entity.Property(x => x.Sube).HasMaxLength(100);
            entity.Property(x => x.Not).HasMaxLength(1000);
            entity.Property(x => x.TekillikAnahtari).HasMaxLength(320);
            entity.Property(x => x.GuncelleyenKullanici).HasMaxLength(100);

            entity.HasIndex(x => new { x.Tur, x.FirmaId });

            // SGK: aynı firma + şube bir kez (diğer türlerde anahtar null). HasFilter açıkça: null'lar çakışmasın.
            entity.HasIndex(x => x.TekillikAnahtari).IsUnique().HasFilter("[TekillikAnahtari] IS NOT NULL");

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.SetNull);
        }
    }

    /// <summary>Parola erişim kaydı — Parola'ya FK YOK: kayıt silinince erişim kayıtları kalır.</summary>
    public class ParolaErisimKaydiEntityTypeConfiguration : IEntityTypeConfiguration<ParolaErisimKaydi>
    {
        public void Configure(EntityTypeBuilder<ParolaErisimKaydi> entity)
        {
            entity.ToTable("ParolaErisimKayitlari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Tur).HasConversion<byte>();
            entity.Property(x => x.FirmaAdi).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Alan).HasMaxLength(100);
            entity.Property(x => x.Islem).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Kullanici).HasMaxLength(100);

            entity.HasIndex(x => new { x.ParolaId, x.Tarih });
            entity.HasIndex(x => x.Tarih);
        }
    }
}
