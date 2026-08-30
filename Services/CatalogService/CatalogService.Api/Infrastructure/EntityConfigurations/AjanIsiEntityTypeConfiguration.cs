using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Ajan işleri tablosu.
    ///
    /// Kapsam kolonu <c>FirmaId</c> (catalog.Firmalar.Id) — banka otomasyonundaki
    /// diğer tablolarla aynı; yabancı anahtar kısıtı orada olduğu gibi burada da
    /// konmadı.
    /// </summary>
    public class AjanIsiEntityTypeConfiguration : IEntityTypeConfiguration<AjanIsi>
    {
        public void Configure(EntityTypeBuilder<AjanIsi> builder)
        {
            builder.ToTable("AjanIsleri", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            // Kimliği sunucu üretiyor; veritabanının değer üretmesi beklenmesin.
            builder.Property(x => x.Id).ValueGeneratedNever();

            builder.Property(x => x.FirmaId).IsRequired();
            builder.Property(x => x.AjanId).IsRequired().HasMaxLength(64);
            builder.Property(x => x.IsTipi).IsRequired().HasMaxLength(50);
            // Yuk ve SonucOzeti: JSON, uzunluk sınırsız (nvarchar(max)).
            builder.Property(x => x.IlerlemeMesaji).HasMaxLength(300);
            builder.Property(x => x.OlusturanKullaniciId).HasMaxLength(64);
            builder.Property(x => x.HataMesaji).HasMaxLength(2000);
            builder.Property(x => x.HataEkraniDosyaId).HasMaxLength(100);

            builder.Property(x => x.OlusturmaZamani).HasColumnType("datetime2");
            builder.Property(x => x.GonderimZamani).HasColumnType("datetime2");
            builder.Property(x => x.BaslamaZamani).HasColumnType("datetime2");
            builder.Property(x => x.BitisZamani).HasColumnType("datetime2");
            builder.Property(x => x.SonIlerlemeZamani).HasColumnType("datetime2");

            builder.Ignore(x => x.Bitti);
            builder.Ignore(x => x.Acik);

            // "Bu ajanın açık işi var mı" ve "ajan bağlandı, bekleyenleri gönder"
            // sorgularının ikisi de bu indeksten geçiyor.
            builder.HasIndex(x => new { x.AjanId, x.Durum });

            // Aktar ekranının listesi: firma + en yeni.
            builder.HasIndex(x => new { x.FirmaId, x.OlusturmaZamani });
        }
    }
}
