using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MevzuatKartEntityTypeConfiguration : IEntityTypeConfiguration<MevzuatKart>
    {
        public void Configure(EntityTypeBuilder<MevzuatKart> builder)
        {
            builder.ToTable("MevzuatKartlar", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            // Enum byte olarak saklanır (MevzuatKartTuru : byte).
            builder.Property(x => x.Tur).IsRequired();

            builder.Property(x => x.Baslik)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(x => x.Madde)
                   .HasMaxLength(100);

            builder.Property(x => x.Ozet)
                   .HasMaxLength(1000);

            builder.HasIndex(x => x.Tur);
        }
    }
}
