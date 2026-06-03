using CatalogService.Api.Features.Banka.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class HesapEntityTypeConfiguration : IEntityTypeConfiguration<Hesap>
    {
        public void Configure(EntityTypeBuilder<Hesap> builder)
        {
            builder.ToTable("Hesaplar");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Ad)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Tip)
                .HasConversion<int>();

            builder.Property(x => x.Siklik)
                .HasConversion<int>();

            // Firma'ya FK. Firma kayıtları soft-delete (Aktif) ile yönetildiği için
            // kazara toplu silmeyi engellemek adına Restrict.
            builder.HasOne<Firma>()
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.FirmaId);
            builder.HasIndex(x => x.AktifMi);
        }
    }
}
