using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Sistemler.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Kullanılan sistemler (Prompt 9). <c>Sistemler</c> global: firmadan ve tenant'tan
    /// bağımsız ortak liste, query filter yok.
    /// </summary>
    public class SistemEntityTypeConfiguration : IEntityTypeConfiguration<Sistem>
    {
        public void Configure(EntityTypeBuilder<Sistem> entity)
        {
            entity.ToTable("Sistemler", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Ad).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Tur).HasConversion<byte>();
            entity.Property(x => x.OlusturanKullaniciId).HasMaxLength(64);

            // Benzer ad uyarısı yazımı yakalar; aynı adın aynı türde ikinci kez açılmasını
            // ise veritabanı engeller (uyarıya ısrar edilse bile birebir aynı ad olmaz).
            entity.HasIndex(x => new { x.Tur, x.Ad }).IsUnique();
        }
    }

    public class FirmaSistemiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaSistemi>
    {
        public void Configure(EntityTypeBuilder<FirmaSistemi> entity)
        {
            entity.ToTable("FirmaSistemleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FirmaKodu).HasMaxLength(50);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);

            // Sistem silinmez (birleştirilir ya da pasife alınır); atamalı sistemin silinmesini engelle.
            entity.HasOne<Sistem>().WithMany().HasForeignKey(x => x.SistemId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.FirmaId, x.SistemId }).IsUnique();
            entity.HasIndex(x => x.SistemId);
        }
    }
}
