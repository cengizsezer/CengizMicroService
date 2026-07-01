using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FirmaKontrolMizanSatirEntityTypeConfiguration : IEntityTypeConfiguration<FirmaKontrolMizanSatir>
    {
        public void Configure(EntityTypeBuilder<FirmaKontrolMizanSatir> builder)
        {
            builder.ToTable("FirmaKontrolMizanSatirlari");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Kod)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(x => x.Ad)
                .HasMaxLength(200);

            builder.Property(x => x.Bakiye)
                .HasPrecision(18, 2);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            // (Firma, Dönem, Yıl, Kod) tekil — idempotent yükleme bu birleşim üzerinden çalışır.
            builder.HasIndex(x => new { x.FirmaId, x.Donem, x.Yil, x.Kod }).IsUnique();
        }
    }
}
