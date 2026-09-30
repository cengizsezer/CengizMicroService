using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FirmaKontrolMizanAgacEntityTypeConfiguration : IEntityTypeConfiguration<FirmaKontrolMizanAgac>
    {
        public void Configure(EntityTypeBuilder<FirmaKontrolMizanAgac> builder)
        {
            builder.ToTable("FirmaKontrolMizanAgaclari");

            builder.HasKey(x => x.Id);

            // Birkaç bin düğümlük liste tek alanda durur; uzunluk sınırı konmaz.
            builder.Property(x => x.DugumlerJson)
                .IsRequired();

            builder.Property(x => x.BorcToplam).HasColumnType("decimal(18,2)");
            builder.Property(x => x.AlacakToplam).HasColumnType("decimal(18,2)");

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ham mizan satırlarıyla AYNI anahtar: firma + dönem + yıl başına tek ağaç.
            // Yükleme idempotenttir; bu birleşim üzerinden sil + yaz yapılır.
            builder.HasIndex(x => new { x.FirmaId, x.Donem, x.Yil }).IsUnique();
        }
    }
}
