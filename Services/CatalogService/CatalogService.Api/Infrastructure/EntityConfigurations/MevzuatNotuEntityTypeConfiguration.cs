using CatalogService.Api.Features.MevzuatNotlari.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MevzuatNotuEntityTypeConfiguration : IEntityTypeConfiguration<MevzuatNotu>
    {
        public void Configure(EntityTypeBuilder<MevzuatNotu> builder)
        {
            builder.ToTable("MevzuatNotlari", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Kategori)
                   .IsRequired()
                   .HasMaxLength(10);

            builder.Property(x => x.MaddeNo)
                   .HasMaxLength(50);

            builder.Property(x => x.Baslik)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(x => x.Ozet)
                   .HasMaxLength(300);

            // Icerik: markdown, uzunluk sınırsız (nvarchar(max)).

            builder.Property(x => x.Etiketler)
                   .HasMaxLength(200);

            builder.Property(x => x.Kaynak)
                   .HasMaxLength(500);

            builder.HasIndex(x => x.Kategori);
        }
    }
}
