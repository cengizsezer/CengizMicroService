using CatalogService.Api.Features.KdvBeyanname.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class GelenFaturaPdfConfiguration : IEntityTypeConfiguration<GelenFaturaPdf>
    {
        public void Configure(EntityTypeBuilder<GelenFaturaPdf> builder)
        {
            builder.ToTable("GelenFaturaPdfleri");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.FaturaNo).IsRequired().HasMaxLength(50);
            builder.Property(x => x.FileName).HasMaxLength(260);

            // Aynı firma+fatura için tek PDF eşlemesi — tekrar çekmeyi önlemenin anahtarı.
            builder.HasIndex(x => new { x.FirmaId, x.FaturaNo }).IsUnique();
        }
    }
}
