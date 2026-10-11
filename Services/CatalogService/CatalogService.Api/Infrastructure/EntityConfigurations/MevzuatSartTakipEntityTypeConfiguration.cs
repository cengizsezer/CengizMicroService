using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MevzuatSartTakipEntityTypeConfiguration : IEntityTypeConfiguration<MevzuatSartTakip>
    {
        public void Configure(EntityTypeBuilder<MevzuatSartTakip> builder)
        {
            builder.ToTable("MevzuatSartTakipleri", CatalogContext.DEFAULT_SCHEMA);

            // (FirmaKartId, SartKartId) birincil anahtar = çift başına tek kayıt.
            builder.HasKey(x => new { x.FirmaKartId, x.SartKartId });

            builder.HasIndex(x => x.SartKartId);

            // Aynı tabloya iki FK: Restrict; temizlik serviste (bkz. MevzuatBaglanti).
            builder.HasOne<MevzuatKart>()
                   .WithMany()
                   .HasForeignKey(x => x.FirmaKartId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MevzuatKart>()
                   .WithMany()
                   .HasForeignKey(x => x.SartKartId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
