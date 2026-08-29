using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Firma Bilgileri tabloları. Kapsam <c>catalog.Firmalar.Id</c>; global query filter
    /// <b>kurulmadı</b> — kapsam her sorguda görünür yazılıyor (Banka Otomasyon'daki
    /// karar, KARARLAR §68–§72).
    /// </summary>
    public class FirmaSicilBilgisiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaSicilBilgisi>
    {
        public void Configure(EntityTypeBuilder<FirmaSicilBilgisi> entity)
        {
            entity.ToTable("FirmaSicilBilgileri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.MersisNo).HasMaxLength(30);
            entity.Property(x => x.Adres).HasMaxLength(500);
            entity.Property(x => x.NaceKodu).HasMaxLength(20);
            entity.Property(x => x.SermayeParaBirimi).HasMaxLength(3);
            entity.Property(x => x.Sermaye).HasColumnType("decimal(18,2)");

            // Firma başına tek sicil kaydı.
            entity.HasIndex(x => x.FirmaId).IsUnique();
        }
    }

    public class FirmaOrtakEntityTypeConfiguration : IEntityTypeConfiguration<FirmaOrtak>
    {
        public void Configure(EntityTypeBuilder<FirmaOrtak> entity)
        {
            entity.ToTable("FirmaOrtaklari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Ad).IsRequired().HasMaxLength(200);
            entity.Property(x => x.TcknVkn).HasMaxLength(11);
            entity.Property(x => x.Not).HasMaxLength(500);
            entity.Property(x => x.PayTutari).HasColumnType("decimal(18,2)");
            entity.Property(x => x.PayOrani).HasColumnType("decimal(9,4)");

            entity.HasIndex(x => x.FirmaId);
        }
    }

    public class FirmaImzaYetkilisiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaImzaYetkilisi>
    {
        public void Configure(EntityTypeBuilder<FirmaImzaYetkilisi> entity)
        {
            entity.ToTable("FirmaImzaYetkilileri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Ad).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Tckn).HasMaxLength(11);
            entity.Property(x => x.Gorev).HasMaxLength(150);
            entity.Property(x => x.Not).HasMaxLength(500);

            entity.HasIndex(x => x.FirmaId);
        }
    }

    public class FirmaBelgesiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaBelgesi>
    {
        public void Configure(EntityTypeBuilder<FirmaBelgesi> entity)
        {
            entity.ToTable("FirmaBelgeleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            entity.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Aciklama).HasMaxLength(300);
            entity.Property(x => x.YukleyenKullanici).HasMaxLength(100);

            // Aynı türden birden çok belge olabilir (vergi levhası her yıl yenileniyor);
            // benzersizlik kısıtı YOK, yalnız arama indeksi var.
            entity.HasIndex(x => new { x.FirmaId, x.Tur });
        }
    }
}
