using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FirmaKontrolVergiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaKontrolVergi>
    {
        public void Configure(EntityTypeBuilder<FirmaKontrolVergi> builder)
        {
            builder.ToTable("FirmaKontrolVergiler");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Kkeg).HasPrecision(18, 2);
            builder.Property(x => x.KkegIstisna).HasPrecision(18, 2);
            builder.Property(x => x.GecmisYil_2024).HasPrecision(18, 2);
            builder.Property(x => x.GecmisYil_2023).HasPrecision(18, 2);
            builder.Property(x => x.GecmisYil_2022).HasPrecision(18, 2);
            builder.Property(x => x.GecmisYil_2021).HasPrecision(18, 2);
            builder.Property(x => x.TemettuGeliri).HasPrecision(18, 2);
            builder.Property(x => x.BagisYardim).HasPrecision(18, 2);
            builder.Property(x => x.Kv5Indirim).HasPrecision(18, 2);
            builder.Property(x => x.GeciciVergi).HasPrecision(18, 2);
            builder.Property(x => x.BankaStopaji).HasPrecision(18, 2);
            builder.Property(x => x.DigerTevkifat).HasPrecision(18, 2);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            // (Firma, Dönem, Yıl) tekil — firma başına dönem/yıl için tek girdi satırı (upsert).
            builder.HasIndex(x => new { x.FirmaId, x.Donem, x.Yil }).IsUnique();
        }
    }
}
