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
            // Hesabın ORKA'daki adı; elle açılmış eski kayıtlarda boş olabildiği için nullable.
            builder.Property(x => x.HesapAdi).HasMaxLength(200);
            builder.Property(x => x.ParaBirimi).IsRequired().HasMaxLength(3);
            builder.Property(x => x.Iban).HasMaxLength(34);
            // Boşluklu ORKA kodu; format değiştirilmeden saklanır.
            builder.Property(x => x.OrkaHesapKodu).IsRequired().HasMaxLength(30);
            builder.Property(x => x.ParserTipi).IsRequired().HasMaxLength(50);

            builder.HasIndex(x => new { x.TenantNo, x.BankaAdi });

            // Toplu içe aktarımın upsert anahtarı; tekillik servis katmanında da kontrol
            // ediliyor, index yarış durumunda son savunma.
            builder.HasIndex(x => new { x.TenantNo, x.OrkaHesapKodu }).IsUnique();
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
            // DosyaIcerik: kaynak xlsx (varbinary(max)); düzeltilmiş ekstre bundan üretilir.

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
            builder.Property(x => x.AnahtarCekirdek).HasMaxLength(200);
            builder.Property(x => x.AyirtEdiciEk).HasMaxLength(60);
            // Adaylar: aile üyelerinin JSON listesi, uzunluk sınırsız (nvarchar(max)).

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

    /// <summary>
    /// Öğrenilen eşleşmeler — firma bazlı. Anahtar ham hash değil, unvan çekirdeği;
    /// aynı çekirdeği paylaşan cari ailesinde ayırt edici ek anahtarın parçasıdır.
    /// </summary>
    public class HesapEslesmesiEntityTypeConfiguration : IEntityTypeConfiguration<HesapEslesmesi>
    {
        public void Configure(EntityTypeBuilder<HesapEslesmesi> builder)
        {
            builder.ToTable("EkstreHesapEslesmeleri", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(20);
            builder.Property(x => x.AnahtarCekirdek).IsRequired().HasMaxLength(200);
            builder.Property(x => x.AyirtEdiciEk).HasMaxLength(60);
            builder.Property(x => x.HesapKodu).IsRequired().HasMaxLength(30);
            builder.Property(x => x.HesapAdi).HasMaxLength(200);
            builder.Property(x => x.SonKullanim).HasColumnType("datetime2");

            builder.Ignore(x => x.TamAnahtar);

            // Aynı firma + anahtar (çekirdek + ek) + tip + yön tek kayıt.
            // AyirtEdiciEk nullable olduğu için unique index filtresiz kurulamaz; SQL Server'da
            // NULL'lar tekil sayılır, bu yüzden ek dolu ve boş olan iki ayrı index kullanılıyor.
            builder.HasIndex(x => new { x.TenantNo, x.AnahtarTipi, x.AnahtarCekirdek, x.Yon })
                   .IsUnique()
                   .HasFilter("[AyirtEdiciEk] IS NULL")
                   .HasDatabaseName("IX_EkstreHesapEslesmeleri_Cekirdek");

            builder.HasIndex(x => new { x.TenantNo, x.AnahtarTipi, x.AnahtarCekirdek, x.AyirtEdiciEk, x.Yon })
                   .IsUnique()
                   .HasFilter("[AyirtEdiciEk] IS NOT NULL")
                   .HasDatabaseName("IX_EkstreHesapEslesmeleri_CekirdekEk");
        }
    }

    /// <summary>
    /// Karşı tarafın kimliği — global. Firma filtresi yok: bir unvanın kim olduğu
    /// her firmada aynıdır, Aday'da öğrenilen SMMM'de hazır gelir.
    /// </summary>
    public class KimlikKaydiEntityTypeConfiguration : IEntityTypeConfiguration<KimlikKaydi>
    {
        public void Configure(EntityTypeBuilder<KimlikKaydi> builder)
        {
            builder.ToTable("EkstreKimlikKayitlari", CatalogContext.DEFAULT_SCHEMA);
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Anahtar).IsRequired().HasMaxLength(200);
            builder.Property(x => x.NormalizeUnvan).HasMaxLength(200);
            builder.Property(x => x.SonKullanim).HasColumnType("datetime2");

            builder.HasIndex(x => new { x.AnahtarTipi, x.Anahtar }).IsUnique();
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
            builder.Property(x => x.SonGuncelleme).HasColumnType("datetime2");

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
