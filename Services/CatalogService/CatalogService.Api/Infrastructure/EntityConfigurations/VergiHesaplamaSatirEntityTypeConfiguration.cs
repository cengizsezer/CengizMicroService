using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class VergiHesaplamaSatirEntityTypeConfiguration : IEntityTypeConfiguration<VergiHesaplamaSatir>
    {
        public void Configure(EntityTypeBuilder<VergiHesaplamaSatir> builder)
        {
            builder.ToTable("VergiHesaplamaSatirlari");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Tutar).HasPrecision(19, 2);
            builder.Property(x => x.OncekiDonem).HasPrecision(19, 2);
            builder.Property(x => x.Aciklama).HasMaxLength(500);

            // Kalem silinse bile satır zincirleme silinmesin; kalem zaten pasife alınır.
            builder.HasOne(x => x.VergiKalemi)
                   .WithMany()
                   .HasForeignKey(x => x.VergiKalemiId)
                   .OnDelete(DeleteBehavior.NoAction);

            // Bir beyannamede aynı kalem iki kez yer alamaz.
            builder.HasIndex(x => new { x.HesaplamaId, x.VergiKalemiId }).IsUnique();
        }
    }
}
