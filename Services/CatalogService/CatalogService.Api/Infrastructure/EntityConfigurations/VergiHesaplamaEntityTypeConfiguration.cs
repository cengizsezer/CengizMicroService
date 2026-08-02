using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class VergiHesaplamaEntityTypeConfiguration : IEntityTypeConfiguration<VergiHesaplama>
    {
        public void Configure(EntityTypeBuilder<VergiHesaplama> builder)
        {
            builder.ToTable("VergiHesaplamalar");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TicariKar).HasPrecision(19, 2);
            builder.Property(x => x.KvOrani).HasPrecision(5, 2).HasDefaultValue(25.00m);
            builder.Property(x => x.IndirimliOran).HasPrecision(5, 2);
            builder.Property(x => x.IndirimliOranMatrahi).HasPrecision(19, 2);
            builder.Property(x => x.Notlar).HasMaxLength(2000);

            builder.HasOne(x => x.Firma)
                   .WithMany()
                   .HasForeignKey(x => x.FirmaId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Firma başına dönem yılı için tek beyanname.
            builder.HasIndex(x => new { x.FirmaId, x.DonemYil }).IsUnique();

            builder.HasMany(x => x.Satirlar)
                   .WithOne(x => x.Hesaplama)
                   .HasForeignKey(x => x.HesaplamaId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.GecmisYilZararlari)
                   .WithOne(x => x.Hesaplama)
                   .HasForeignKey(x => x.HesaplamaId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
