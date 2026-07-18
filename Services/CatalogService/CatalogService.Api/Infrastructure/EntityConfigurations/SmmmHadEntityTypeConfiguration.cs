using CatalogService.Api.Features.SmmmTakip.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class SmmmHadEntityTypeConfiguration : IEntityTypeConfiguration<SmmmHad>
    {
        public void Configure(EntityTypeBuilder<SmmmHad> builder)
        {
            builder.ToTable("SmmmHadler", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Kod)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(x => x.Ad)
                   .IsRequired()
                   .HasMaxLength(250);

            builder.Property(x => x.Birim)
                   .IsRequired()
                   .HasMaxLength(10);

            builder.Property(x => x.Dayanak)
                   .HasMaxLength(200);

            builder.HasIndex(x => x.Kod).IsUnique();
            builder.HasIndex(x => x.KonuId);

            // Had silinince değerleri de silinir (Cascade).
            builder.HasMany(x => x.Degerler)
                   .WithOne()
                   .HasForeignKey(x => x.HadId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
