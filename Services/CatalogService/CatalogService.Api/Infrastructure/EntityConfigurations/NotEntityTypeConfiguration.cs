using CatalogService.Api.Features.Banka.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class NotEntityTypeConfiguration : IEntityTypeConfiguration<Not>
    {
        public void Configure(EntityTypeBuilder<Not> builder)
        {
            builder.ToTable("HesapNotlari");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Kapsam)
                .HasConversion<int>();

            builder.Property(x => x.Tarih)
                .HasColumnType("date");

            builder.Property(x => x.Metin)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(x => x.OlusturanKullanici)
                .HasMaxLength(150);

            // Hesap'a FK. Hesap silinirse notları da silinir.
            builder.HasOne<Hesap>()
                .WithMany()
                .HasForeignKey(x => x.HesapId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.HesapId);
        }
    }
}
