using CatalogService.Api.Features.KdvBeyanname.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class KdvBeyannameTaramaConfiguration : IEntityTypeConfiguration<KdvBeyannameTarama>
    {
        public void Configure(EntityTypeBuilder<KdvBeyannameTarama> builder)
        {
            builder.ToTable("KdvBeyannameTaramalar");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Durum).IsRequired();
            builder.Property(x => x.HataMesaji).HasMaxLength(2000);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.FirmaId, x.BaslangicAt });
            builder.HasIndex(x => x.Durum);
        }
    }
}
