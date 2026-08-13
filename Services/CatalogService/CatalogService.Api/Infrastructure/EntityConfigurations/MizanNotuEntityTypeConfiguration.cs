using CatalogService.Api.Features.FirmaKontrol.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MizanNotuEntityTypeConfiguration : IEntityTypeConfiguration<MizanNotu>
    {
        public void Configure(EntityTypeBuilder<MizanNotu> builder)
        {
            builder.ToTable("MizanNotlari");

            builder.HasKey(x => x.Id);

            // Mizan satırının Kod alanı ile aynı genişlik — alt kırılım ("381.01") sığar.
            builder.Property(x => x.HesapKodu)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(x => x.Metin)
                .IsRequired()
                .HasMaxLength(2000);

            // Mizan bakiyeleriyle aynı hassasiyet (FirmaKontrolMizanSatir.Bakiye).
            builder.Property(x => x.SnapshotBorc).HasPrecision(18, 2);
            builder.Property(x => x.SnapshotAlacak).HasPrecision(18, 2);
            builder.Property(x => x.SnapshotBakiye).HasPrecision(18, 2);

            builder.HasOne(x => x.Firma)
                .WithMany()
                .HasForeignKey(x => x.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Hesap başına bir kalıcı (DonemYili NULL) + yıl başına bir dönem notu.
            // HasFilter(null) ŞART: EF nullable kolonlu unique index'e varsayılan olarak
            // "[DonemYili] IS NOT NULL" filtresi ekler, o zaman kalıcı notlar kapsam dışı
            // kalıp aynı hesaba birden çok kalıcı not yazılabilirdi. Filtresiz index'te
            // SQL Server NULL'ları eşit sayar ve kalıcı not de tekilleşir.
            // FirmaId önek olduğundan firmanın tüm notlarını çeken sorgu da bunu kullanır.
            builder.HasIndex(x => new { x.FirmaId, x.HesapKodu, x.DonemYili })
                .IsUnique()
                .HasFilter(null);
        }
    }
}
