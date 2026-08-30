using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.KdvBeyanname.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FirmaEntityTypeConfiguration : IEntityTypeConfiguration<Firma>
    {
        public void Configure(EntityTypeBuilder<Firma> builder)
        {
            builder.ToTable("Firmalar");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.VergiKimlikNo)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(x => x.Unvan)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(x => x.KisaAd)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Email)
                .HasMaxLength(150);

            builder.Property(x => x.Telefon)
                .HasMaxLength(30);

            builder.Property(x => x.TicaretSicilNo)
                .HasMaxLength(50);

            builder.Property(x => x.VergiDairesi)
                .HasMaxLength(100);

            // ORKA firma kodu; giris zincirinde kullanilan kisa kod ("0001").
            builder.Property(x => x.OrkaFirmaKodu).HasMaxLength(20);

            builder.Property(x => x.VergiDairesiKodu)
                .HasMaxLength(10);

            builder.Property(x => x.YetkiliAdi)
                .HasMaxLength(100);

            builder.Property(x => x.YetkiliSoyadi)
                .HasMaxLength(100);

            builder.Property(x => x.TelefonAlanKodu)
                .HasMaxLength(10);

            builder.HasIndex(x => x.VergiKimlikNo).IsUnique();
            builder.HasIndex(x => x.Aktif);

            builder.HasOne<Duzenleyen>()
                .WithMany()
                .HasForeignKey(x => x.DuzenleyenId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasIndex(x => x.DuzenleyenId);
        }
    }
}
