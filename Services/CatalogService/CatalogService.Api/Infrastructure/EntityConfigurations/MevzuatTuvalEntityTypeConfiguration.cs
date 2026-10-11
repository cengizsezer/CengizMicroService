using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MevzuatTuvalEntityTypeConfiguration : IEntityTypeConfiguration<MevzuatTuval>
    {
        public void Configure(EntityTypeBuilder<MevzuatTuval> builder)
        {
            builder.ToTable("MevzuatTuvaller", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Ad)
                   .IsRequired()
                   .HasMaxLength(100);

            // Tuval silinince yerleşimleri de silinir (kartlar kalır).
            builder.HasMany(x => x.Kartlar)
                   .WithOne()
                   .HasForeignKey(x => x.TuvalId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
