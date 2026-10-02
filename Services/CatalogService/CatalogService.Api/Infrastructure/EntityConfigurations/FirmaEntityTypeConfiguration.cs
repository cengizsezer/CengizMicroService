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

            // Sınıflandırma: enum'lar byte; mevcut kayıtlar Belirsiz/Yok (0) ile gelir.
            builder.Property(x => x.DefterUsulu).HasConversion<byte>();
            builder.Property(x => x.DefterUsuluKaynagi).HasConversion<byte>();
            builder.Property(x => x.VergiTuru).HasConversion<byte>();
            builder.Property(x => x.VergiTuruKaynagi).HasConversion<byte>();
            builder.Property(x => x.MizanFormati).HasMaxLength(50);
            builder.Property(x => x.HesapDonemi).HasConversion<byte?>();
            builder.Property(x => x.OzelDonemBas).HasColumnType("date");
            builder.Property(x => x.OzelDonemBit).HasColumnType("date");
            builder.Property(x => x.FirmaTipi).HasConversion<byte>();
            builder.Property(x => x.SgkTesvikKademesi).HasConversion<byte>();
            builder.Property(x => x.MuhtasarDonemi).HasConversion<byte>();

            // Kullanılan sistemler kartının serbest notu.
            builder.Property(x => x.SistemNotu).HasMaxLength(500);

            // Sorumlu: IdentityService kullanıcısı; servisler arası FK yok, ad anlık görüntü.
            builder.Property(x => x.SorumluKullaniciAdi).HasMaxLength(100);
            builder.HasIndex(x => x.SorumluKullaniciId);

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
