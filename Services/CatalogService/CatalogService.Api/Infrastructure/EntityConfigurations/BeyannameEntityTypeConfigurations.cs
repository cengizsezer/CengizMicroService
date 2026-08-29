using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Beyanname türü tanımları. Tablo <b>global</b>: tenant kolonu yok, beyanname türleri
    /// bütün firmalarda aynı.
    /// </summary>
    public class BeyannameTuruEntityTypeConfiguration : IEntityTypeConfiguration<BeyannameTuru>
    {
        public void Configure(EntityTypeBuilder<BeyannameTuru> entity)
        {
            entity.ToTable("BeyannameTurleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Deger).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Kod).HasMaxLength(20);
            entity.Property(x => x.Ad).IsRequired().HasMaxLength(150);

            // Saklanan değer eşleştirmenin anahtarı; iki kayıt aynı değeri taşırsa
            // kaydın hangi kolona düşeceği kayıt sırasına kalırdı.
            entity.HasIndex(x => x.Deger).IsUnique();
        }
    }

    /// <summary>
    /// Beyanname belgeleri. Dosyanın kendisi FileApiService'te; burada yalnız FileId ve
    /// metadata (JobAttachment / TicaretSicilEk kalıbı).
    /// </summary>
    public class BeyannameEkEntityTypeConfiguration : IEntityTypeConfiguration<BeyannameEk>
    {
        public void Configure(EntityTypeBuilder<BeyannameEk> entity)
        {
            entity.ToTable("BeyannameEkleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            entity.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.YukleyenKullanici).HasMaxLength(100);

            entity.HasOne(x => x.Declaration)
                  .WithMany()
                  .HasForeignKey(x => x.DeclarationId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Her (beyanname, tür) için tek belge: ikonun hangi dosyayı açacağı belirsiz
            // kalmasın. Servis de aynı kuralı uyguluyor (ikinci yükleme eskisinin yerine geçer).
            entity.HasIndex(x => new { x.DeclarationId, x.Tur }).IsUnique();
        }
    }
}
