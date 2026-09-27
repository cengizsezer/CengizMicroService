using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class EtiketBoyutuEntityTypeConfiguration : IEntityTypeConfiguration<EtiketBoyutu>
    {
        public void Configure(EntityTypeBuilder<EtiketBoyutu> builder)
        {
            builder.ToTable("EtiketBoyutlari");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Ad).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Kapsam).HasConversion<byte>();

            // "600,601,610" ya da "6*" — kapsam paydasını bu belirler, boş bırakılamaz.
            builder.Property(x => x.KapsamHesaplari).IsRequired().HasMaxLength(400);

            // Kapsam = TumFirmalar ise FirmaId null kalır; bu yüzden ilişki opsiyoneldir.
            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.FirmaId, x.Sira });
        }
    }

    public class EtiketDegeriEntityTypeConfiguration : IEntityTypeConfiguration<EtiketDegeri>
    {
        public void Configure(EntityTypeBuilder<EtiketDegeri> builder)
        {
            builder.ToTable("EtiketDegerleri");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Ad).IsRequired().HasMaxLength(100);

            // "#RRGGBB" ya da "#RRGGBBAA".
            builder.Property(x => x.Renk).HasMaxLength(9);

            builder.HasOne(x => x.Boyut)
                .WithMany(b => b.Degerler)
                .HasForeignKey(x => x.BoyutId)
                .OnDelete(DeleteBehavior.Cascade);

            // Aynı boyutta aynı ad iki kez tanımlanmasın.
            builder.HasIndex(x => new { x.BoyutId, x.Ad }).IsUnique();
            builder.HasIndex(x => new { x.BoyutId, x.Sira });

            // FirmaId'ye BİLEREK FK kurulmuyor (EtiketKurali.FirmaId ile aynı gerekçe:
            // Değer → Boyut → Firma cascade yolu zaten var, ikincisi 1785 hatası verir).
            builder.HasIndex(x => x.FirmaId);
        }
    }

    public class EtiketKuraliEntityTypeConfiguration : IEntityTypeConfiguration<EtiketKurali>
    {
        public void Configure(EntityTypeBuilder<EtiketKurali> builder)
        {
            builder.ToTable("EtiketKurallari");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Desen).IsRequired().HasMaxLength(200);
            builder.Property(x => x.EslesmeTipi).HasConversion<byte>();

            builder.HasOne(x => x.Boyut)
                .WithMany(b => b.Kurallar)
                .HasForeignKey(x => x.BoyutId)
                .OnDelete(DeleteBehavior.Cascade);

            // Boyut silinince kurallar zaten gidiyor; değer üzerinden ikinci bir cascade
            // yolu SQL Server'da döngü sayılır, o yüzden burada Restrict.
            builder.HasOne(x => x.Deger)
                .WithMany()
                .HasForeignKey(x => x.DegerId)
                .OnDelete(DeleteBehavior.Restrict);

            // FirmaId'ye BİLEREK FK kurulmuyor. Kural → Boyut (Cascade) ve Boyut → Firma
            // (Cascade) zaten var; buraya ikinci bir Firma FK'sı eklenince Firmalar'dan
            // EtiketKurallari'na iki cascade yolu oluşur ve SQL Server tabloyu yaratmayı
            // reddeder (1785: multiple cascade paths). Alan yalnızca süzme içindir.

            // Kural değerlendirme sırası: boyut içinde Sira'ya göre.
            builder.HasIndex(x => new { x.BoyutId, x.Sira });
            builder.HasIndex(x => x.FirmaId);
        }
    }
}
