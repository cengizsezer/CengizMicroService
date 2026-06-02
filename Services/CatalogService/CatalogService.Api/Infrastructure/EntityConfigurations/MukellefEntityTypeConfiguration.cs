using CatalogService.Api.Features.Mukellefler.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MukellefEntityTypeConfiguration : IEntityTypeConfiguration<Mukellef>
    {
        public void Configure(EntityTypeBuilder<Mukellef> builder)
        {
            builder.ToTable("Mukellefler");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.SozlesmeNo)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Unvan)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(x => x.VergiKimlikNo)
                .IsRequired()
                .HasMaxLength(11);

            builder.Property(x => x.Durum)
                .HasConversion<int>();

            builder.Property(x => x.Hizmet)
                .HasMaxLength(200);

            builder.Property(x => x.Telefon)
                .HasMaxLength(30);

            builder.Property(x => x.Email)
                .HasMaxLength(150);

            // Finans risk alanları (hepsi nullable)
            builder.Property(x => x.AcikBakiye)
                .HasPrecision(18, 2);

            builder.Property(x => x.FinansAciklama)
                .HasMaxLength(1000);

            builder.HasIndex(x => x.SozlesmeNo).IsUnique();
            builder.HasIndex(x => x.VergiKimlikNo).IsUnique();
            builder.HasIndex(x => x.Durum);
            builder.HasIndex(x => x.Unvan);
            builder.HasIndex(x => x.FinansSinifi);
        }
    }
}
