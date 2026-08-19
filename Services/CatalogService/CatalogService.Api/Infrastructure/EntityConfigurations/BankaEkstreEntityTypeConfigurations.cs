using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Banka ekstresi işleme modülünün tablo eşlemeleri. Tek dosyada toplandı:
    /// tablolar aynı modülün parçası ve her biri kısa.
    /// </summary>
    public class BankaHesabiEntityTypeConfiguration : IEntityTypeConfiguration<BankaHesabi>
    {
        public void Configure(EntityTypeBuilder<BankaHesabi> builder)
        {
            builder.ToTable("EkstreBankaHesaplari", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(20);
            builder.Property(x => x.BankaAdi).IsRequired().HasMaxLength(100);
            builder.Property(x => x.ParaBirimi).IsRequired().HasMaxLength(3);
            builder.Property(x => x.Iban).HasMaxLength(34);
            // Boşluklu ORKA kodu; format değiştirilmeden saklanır.
            builder.Property(x => x.OrkaHesapKodu).IsRequired().HasMaxLength(30);
            builder.Property(x => x.ParserTipi).IsRequired().HasMaxLength(50);

            builder.HasIndex(x => new { x.TenantNo, x.BankaAdi });
        }
    }

    public class EkstreYuklemeEntityTypeConfiguration : IEntityTypeConfiguration<EkstreYukleme>
    {
        public void Configure(EntityTypeBuilder<EkstreYukleme> builder)
        {
            builder.ToTable("EkstreYuklemeler", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(20);
            builder.Property(x => x.DosyaAdi).IsRequired().HasMaxLength(260);
            builder.Property(x => x.YuklemeTarihi).HasColumnType("datetime2");
            builder.Property(x => x.DonemBaslangic).HasColumnType("date");
            builder.Property(x => x.DonemBitis).HasColumnType("date");
            // Uyarilar: parser uyarıları, uzunluk sınırsız (nvarchar(max)).

            builder.HasOne(x => x.BankaHesabi)
                   .WithMany()
                   .HasForeignKey(x => x.BankaHesabiId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.Satirlar)
                   .WithOne(x => x.EkstreYukleme!)
                   .HasForeignKey(x => x.EkstreYuklemeId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.TenantNo, x.BankaHesabiId });
        }
    }

    public class EkstreSatiriEntityTypeConfiguration : IEntityTypeConfiguration<EkstreSatiri>
    {
        public void Configure(EntityTypeBuilder<EkstreSatiri> builder)
        {
            builder.ToTable("EkstreSatirlari", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Tarih).HasColumnType("date");
            builder.Property(x => x.Tutar).HasColumnType("decimal(18,2)");
            builder.Property(x => x.GuvenSkoru).HasColumnType("decimal(5,4)");
            builder.Property(x => x.IkinciAdaySkoru).HasColumnType("decimal(5,4)");

            builder.Property(x => x.IslemTipi).IsRequired().HasMaxLength(150);
            // HamAciklama: bankada 354 karaktere kadar uzayabiliyor, sınır konmadı.
            builder.Property(x => x.KarsiIban).HasMaxLength(34);
            builder.Property(x => x.KarsiVkn).HasMaxLength(11);
            builder.Property(x => x.Kanal).HasMaxLength(100);

            // ORKA açıklamayı 50 karakterde kesiyor; sınır veritabanında da duruyor.
            builder.Property(x => x.UretilenAciklama).HasMaxLength(50);
            builder.Property(x => x.CikarilanUnvan).HasMaxLength(150);

            builder.Property(x => x.OnerilenHesapKodu).HasMaxLength(30);
            builder.Property(x => x.OnerilenHesapAdi).HasMaxLength(200);
            builder.Property(x => x.IkinciAdayKodu).HasMaxLength(30);
            builder.Property(x => x.IkinciAdayAdi).HasMaxLength(200);
            builder.Property(x => x.OnaylananHesapKodu).HasMaxLength(30);
            builder.Property(x => x.OnaylananHesapAdi).HasMaxLength(200);
            builder.Property(x => x.OnaylayanKullanici).HasMaxLength(100);
            builder.Property(x => x.OnayTarihi).HasColumnType("datetime2");

            builder.Ignore(x => x.EtkinHesapKodu);

            builder.HasIndex(x => new { x.EkstreYuklemeId, x.Durum });
        }
    }

    public class OgrenmeKaydiEntityTypeConfiguration : IEntityTypeConfiguration<OgrenmeKaydi>
    {
        public void Configure(EntityTypeBuilder<OgrenmeKaydi> builder)
        {
            builder.ToTable("EkstreOgrenmeKayitlari", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Anahtar).IsRequired().HasMaxLength(128);
            builder.Property(x => x.HesapKodu).IsRequired().HasMaxLength(30);
            builder.Property(x => x.HesapAdi).HasMaxLength(200);
            builder.Property(x => x.SonKullanim).HasColumnType("datetime2");

            // Aynı anahtar + tip + yön tek kayıt; kullanıcı farklı kod seçerse üzerine yazılır.
            builder.HasIndex(x => new { x.TenantNo, x.AnahtarTipi, x.Anahtar, x.Yon }).IsUnique();
        }
    }

    public class HesapPlaniKaydiEntityTypeConfiguration : IEntityTypeConfiguration<HesapPlaniKaydi>
    {
        public void Configure(EntityTypeBuilder<HesapPlaniKaydi> builder)
        {
            builder.ToTable("EkstreHesapPlani", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Kod).IsRequired().HasMaxLength(30);
            builder.Property(x => x.Ad).IsRequired().HasMaxLength(200);
            builder.Property(x => x.NormalizeAd).IsRequired().HasMaxLength(200);
            builder.Property(x => x.AnaGrup).IsRequired().HasMaxLength(10);
            builder.Property(x => x.BaslangicHarfi).HasMaxLength(1);

            builder.HasIndex(x => new { x.TenantNo, x.Kod }).IsUnique();
            builder.HasIndex(x => new { x.TenantNo, x.AnaGrup, x.BaslangicHarfi });
        }
    }

    public class AciklamaSablonuEntityTypeConfiguration : IEntityTypeConfiguration<AciklamaSablonu>
    {
        public void Configure(EntityTypeBuilder<AciklamaSablonu> builder)
        {
            builder.ToTable("EkstreAciklamaSablonlari", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ParserTipi).IsRequired().HasMaxLength(50);
            builder.Property(x => x.IslemTipiDeseni).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Sablon).IsRequired().HasMaxLength(100);

            builder.HasIndex(x => new { x.ParserTipi, x.Sira });
        }
    }

    public class UnvanDeseniEntityTypeConfiguration : IEntityTypeConfiguration<UnvanDeseni>
    {
        public void Configure(EntityTypeBuilder<UnvanDeseni> builder)
        {
            builder.ToTable("EkstreUnvanDesenleri", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ParserTipi).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Desen).IsRequired().HasMaxLength(400);
            builder.Property(x => x.Aciklama).HasMaxLength(200);

            builder.HasIndex(x => new { x.ParserTipi, x.Sira });
        }
    }

    public class SabitKuralEntityTypeConfiguration : IEntityTypeConfiguration<SabitKural>
    {
        public void Configure(EntityTypeBuilder<SabitKural> builder)
        {
            builder.ToTable("EkstreSabitKurallar", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ParserTipi).IsRequired().HasMaxLength(50);
            builder.Property(x => x.IslemTipiDeseni).IsRequired().HasMaxLength(200);
            builder.Property(x => x.HesapKodu).IsRequired().HasMaxLength(30);
            builder.Property(x => x.HesapAdi).HasMaxLength(200);
            builder.Property(x => x.Guven).HasColumnType("decimal(5,4)");

            builder.HasIndex(x => new { x.ParserTipi, x.Sira });
        }
    }
}
