using CatalogService.Api.Features.Banka.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class IslemKaydiEntityTypeConfiguration : IEntityTypeConfiguration<IslemKaydi>
    {
        public void Configure(EntityTypeBuilder<IslemKaydi> builder)
        {
            builder.ToTable("IslemKayitlari");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Tarih)
                .HasColumnType("date");

            builder.Property(x => x.IsleyenKullanici)
                .HasMaxLength(150);

            // Hesap'a FK. Hesap silinirse ona ait işlem kayıtları da silinir.
            builder.HasOne<Hesap>()
                .WithMany()
                .HasForeignKey(x => x.HesapId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bir hesabın bir günü için en fazla tek kayıt.
            builder.HasIndex(x => new { x.HesapId, x.Tarih }).IsUnique();
        }
    }
}
