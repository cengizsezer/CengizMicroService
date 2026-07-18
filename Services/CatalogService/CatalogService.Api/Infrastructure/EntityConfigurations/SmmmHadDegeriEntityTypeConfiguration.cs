using CatalogService.Api.Features.SmmmTakip.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class SmmmHadDegeriEntityTypeConfiguration : IEntityTypeConfiguration<SmmmHadDegeri>
    {
        public void Configure(EntityTypeBuilder<SmmmHadDegeri> builder)
        {
            builder.ToTable("SmmmHadDegerleri", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Deger)
                   .HasColumnType("decimal(18,2)");

            builder.Property(x => x.Teblig)
                   .HasMaxLength(200);

            builder.Property(x => x.Not)
                   .HasMaxLength(1000);

            // (HadId, Yil) benzersiz — yıl başına tek değer.
            builder.HasIndex(x => new { x.HadId, x.Yil }).IsUnique();
        }
    }
}
