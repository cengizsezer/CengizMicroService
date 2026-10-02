using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    /// <summary>
    /// Firma işleri ve Yapılacaklar tabloları. Kapsam <c>Firmalar.Id</c>; Takip kartı
    /// tablolarındaki gibi global query filter yok, kapsam her sorguda yazılıyor.
    /// </summary>
    public class FirmaIsiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaIsi>
    {
        public void Configure(EntityTypeBuilder<FirmaIsi> entity)
        {
            entity.ToTable("FirmaIsleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Baslik).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Aciklama).HasMaxLength(1000);
            entity.Property(x => x.Tekrar).HasConversion<byte>();
            entity.Property(x => x.GunKurali).HasConversion<byte>();
            entity.Property(x => x.TekSeferTarih).HasColumnType("date");
            entity.Property(x => x.SorumluKullaniciAdi).HasMaxLength(100);
            entity.Property(x => x.OlusturanKullaniciId).HasMaxLength(64);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.FirmaId);

            // Prosedür alanları (Prompt 8).
            entity.Property(x => x.YasalMukellefiyetKodu).HasMaxLength(4);
            entity.Property(x => x.MenuYolu).HasMaxLength(300);

            // Prompt 9: program = ortak sistem listesinden bir kayıt. Sistem silinmez (birleştirilir).
            entity.HasOne<Features.Sistemler.Domain.Sistem>().WithMany().HasForeignKey(x => x.SistemId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.NasilYapilir).HasMaxLength(4000);

            // Bir firmanın bir yasal işi için tek prosedür satırı.
            entity.HasIndex(x => new { x.FirmaId, x.YasalMukellefiyetKodu, x.Tekrar })
                  .IsUnique()
                  .HasFilter("[YasalMukellefiyetKodu] IS NOT NULL");

            // Kendine referans: SQL Server döngülü cascade kabul etmez; silinen işe bağlı
            // ön adım referanslarını servis boşaltır.
            entity.HasOne<FirmaIsi>().WithMany().HasForeignKey(x => x.OnAdimiOlduguIsId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(x => x.Alicilar).WithOne().HasForeignKey(a => a.FirmaIsiId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Ekler).WithOne().HasForeignKey(e => e.FirmaIsiId).OnDelete(DeleteBehavior.Cascade);

            // İş tarifi (Prompt 14). Tarif silinirse iş kendi metniyle devam eder.
            entity.HasOne<IsTarifi>().WithMany().HasForeignKey(x => x.IsTarifiId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.IsTarifiId);
        }
    }

    /// <summary>İş tarifi (Prompt 14) — global, firmadan bağımsız.</summary>
    public class IsTarifiEntityTypeConfiguration : IEntityTypeConfiguration<IsTarifi>
    {
        public void Configure(EntityTypeBuilder<IsTarifi> entity)
        {
            entity.ToTable("IsTarifleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Ad).IsRequired().HasMaxLength(200);
            entity.Property(x => x.MenuYolu).HasMaxLength(300);
            entity.Property(x => x.NasilYapilir).HasMaxLength(4000);

            entity.HasOne<Features.Sistemler.Domain.Sistem>().WithMany().HasForeignKey(x => x.SistemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Ekler).WithOne().HasForeignKey(e => e.IsTarifiId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class IsTarifiEkiEntityTypeConfiguration : IEntityTypeConfiguration<IsTarifiEki>
    {
        public void Configure(EntityTypeBuilder<IsTarifiEki> entity)
        {
            entity.ToTable("IsTarifiEkleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.DosyaAdi).IsRequired().HasMaxLength(260);
            entity.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(x => x.YukleyenKullaniciId).HasMaxLength(64);
            entity.Property(x => x.YukleyenKullaniciAdi).HasMaxLength(100);

            entity.HasIndex(x => x.IsTarifiId);
        }
    }

    public class FirmaIsiAlicisiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaIsiAlicisi>
    {
        public void Configure(EntityTypeBuilder<FirmaIsiAlicisi> entity)
        {
            entity.ToTable("FirmaIsiAlicilari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.AdSoyad).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Eposta).HasMaxLength(200);
            entity.Property(x => x.Rol).HasMaxLength(100);
            entity.Property(x => x.AliciTipi).HasConversion<byte>();

            entity.HasIndex(x => x.FirmaIsiId);

            // Prompt 14: kişi. Kişi silinmez (birleştirilir); SQL Server çoklu cascade yolu (Firma→İş→Alıcı,
            // Firma→Kişi→Alıcı) kabul etmediği için Restrict.
            entity.HasOne(x => x.Kisi).WithMany().HasForeignKey(x => x.KisiId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.KisiId);
        }
    }

    /// <summary>Firmanın kişileri (Prompt 14). Aynı firmada aynı e-posta tek kişi.</summary>
    public class KisiEntityTypeConfiguration : IEntityTypeConfiguration<Kisi>
    {
        public void Configure(EntityTypeBuilder<Kisi> entity)
        {
            entity.ToTable("Kisiler", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Ad).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Eposta).HasMaxLength(200);
            entity.Property(x => x.Rol).HasMaxLength(100);
            entity.Property(x => x.Notu).HasMaxLength(1000);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.FirmaId, x.Eposta }).IsUnique().HasFilter("[Eposta] IS NOT NULL");
        }
    }

    public class FirmaIsiEkiEntityTypeConfiguration : IEntityTypeConfiguration<FirmaIsiEki>
    {
        public void Configure(EntityTypeBuilder<FirmaIsiEki> entity)
        {
            entity.ToTable("FirmaIsiEkleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.DosyaAdi).IsRequired().HasMaxLength(260);
            entity.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(x => x.YukleyenKullaniciId).HasMaxLength(64);
            entity.Property(x => x.YukleyenKullaniciAdi).HasMaxLength(100);

            entity.HasIndex(x => x.FirmaIsiId);
        }
    }

    public class VergiTakvimiEntityTypeConfiguration : IEntityTypeConfiguration<VergiTakvimi>
    {
        public void Configure(EntityTypeBuilder<VergiTakvimi> entity)
        {
            entity.ToTable("VergiTakvimi", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.MukellefiyetKodu).IsRequired().HasMaxLength(4);
            entity.Property(x => x.Ad).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Tekrar).HasConversion<byte>();
            entity.Property(x => x.DonemBas).HasColumnType("date");
            entity.Property(x => x.DonemBit).HasColumnType("date");
            entity.Property(x => x.SonGun).HasColumnType("date");

            // Aynı kodun aynı dönemi iki kez girilmesin (iki satır = aynı iş iki kez).
            entity.HasIndex(x => new { x.MukellefiyetKodu, x.Tekrar, x.Yil, x.DonemNo }).IsUnique();
            entity.HasIndex(x => x.SonGun);
        }
    }

    public class IsTamamlamaEntityTypeConfiguration : IEntityTypeConfiguration<IsTamamlama>
    {
        public void Configure(EntityTypeBuilder<IsTamamlama> entity)
        {
            entity.ToTable("IsTamamlamalari", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.KaynakTip).HasConversion<byte>();
            entity.Property(x => x.DonemAnahtari).IsRequired().HasMaxLength(16);
            entity.Property(x => x.KullaniciId).HasMaxLength(64);
            entity.Property(x => x.KullaniciAdi).HasMaxLength(100);

            entity.HasOne<Firma>().WithMany().HasForeignKey(x => x.FirmaId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.KaynakTip, x.KaynakId, x.FirmaId, x.DonemAnahtari }).IsUnique();
            entity.HasIndex(x => x.FirmaId);

            // Dönem notu (Prompt 10). Yapildi hesaplanır, kolon değil.
            entity.Property(x => x.Not).HasMaxLength(2000);
            entity.Ignore(x => x.Yapildi);
            entity.HasMany(x => x.Ekler).WithOne().HasForeignKey(e => e.IsTamamlamaId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class IsTamamlamaEkiEntityTypeConfiguration : IEntityTypeConfiguration<IsTamamlamaEki>
    {
        public void Configure(EntityTypeBuilder<IsTamamlamaEki> entity)
        {
            entity.ToTable("IsTamamlamaEkleri", CatalogContext.DEFAULT_SCHEMA);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.DosyaAdi).IsRequired().HasMaxLength(260);
            entity.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(x => x.YukleyenKullaniciId).HasMaxLength(64);
            entity.Property(x => x.YukleyenKullaniciAdi).HasMaxLength(100);

            entity.HasIndex(x => x.IsTamamlamaId);
        }
    }
}
