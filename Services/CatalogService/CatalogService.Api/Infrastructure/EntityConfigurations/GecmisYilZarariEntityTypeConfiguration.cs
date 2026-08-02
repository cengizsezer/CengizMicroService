using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class GecmisYilZarariEntityTypeConfiguration : IEntityTypeConfiguration<GecmisYilZarari>
    {
        public void Configure(EntityTypeBuilder<GecmisYilZarari> builder)
        {
            builder.ToTable("GecmisYilZararlari");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ZararTutari).HasPrecision(19, 2);
            builder.Property(x => x.MahsupEdilen).HasPrecision(19, 2);

            // Aynı beyannamede bir yıl bir kez girilir.
            builder.HasIndex(x => new { x.HesaplamaId, x.ZararYili }).IsUnique();
        }
    }
}
