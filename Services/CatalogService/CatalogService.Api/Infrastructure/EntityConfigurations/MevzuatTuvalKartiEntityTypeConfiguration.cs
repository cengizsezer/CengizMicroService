using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MevzuatTuvalKartiEntityTypeConfiguration : IEntityTypeConfiguration<MevzuatTuvalKarti>
    {
        public void Configure(EntityTypeBuilder<MevzuatTuvalKarti> builder)
        {
            builder.ToTable("MevzuatTuvalKartlari", CatalogContext.DEFAULT_SCHEMA);

            // (TuvalId, KartId) birincil anahtar = bir kart bir tuvalde bir kez.
            builder.HasKey(x => new { x.TuvalId, x.KartId });

            builder.HasIndex(x => x.KartId);

            // Tuval ve kart farklı tablolar: iki cascade yolu çakışmaz.
            builder.HasOne<MevzuatKart>()
                   .WithMany()
                   .HasForeignKey(x => x.KartId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
