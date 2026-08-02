using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class VergiKalemiEntityTypeConfiguration : IEntityTypeConfiguration<VergiKalemi>
    {
        public void Configure(EntityTypeBuilder<VergiKalemi> builder)
        {
            builder.ToTable("VergiKalemleri");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Kod).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Ad).IsRequired().HasMaxLength(200);
            builder.Property(x => x.AltGrup).HasMaxLength(100);
            builder.Property(x => x.KanunMaddesi).HasMaxLength(100);
            builder.Property(x => x.Aciklama).HasMaxLength(1000);
            builder.Property(x => x.Hatirlatma).HasMaxLength(1000);
            builder.Property(x => x.OranBilgisi).HasMaxLength(200);
            builder.Property(x => x.UstSinirDeger).HasPrecision(9, 4);

            builder.Property(x => x.Grup).HasConversion<byte>();
            builder.Property(x => x.UstSinirTuru).HasConversion<byte?>();
            builder.Property(x => x.MukellefiyetTuru).HasConversion<byte>();

            builder.HasIndex(x => x.Kod).IsUnique();
            builder.HasIndex(x => new { x.Grup, x.SiraNo });

            // İstisnaya ilişkin KKEG -> büyüteceği istisna kalemi (aynı tabloya self-referans).
            // Silme davranışı NoAction: bağlı istisna silinirse zincirleme silme olmasın.
            builder.HasOne(x => x.BagliIstisnaKalemi)
                   .WithMany()
                   .HasForeignKey(x => x.BagliIstisnaKalemiId)
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
