using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class HesapPlaniEntityTypeConfiguration : IEntityTypeConfiguration<HesapPlani>
    {
        public void Configure(EntityTypeBuilder<HesapPlani> builder)
        {
            builder.ToTable("HesapPlani", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Kod).IsRequired().HasMaxLength(30);
            builder.Property(x => x.KodDuz).IsRequired().HasMaxLength(30);
            builder.Property(x => x.SegmentKod).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Ad).IsRequired().HasMaxLength(150);
            builder.Property(x => x.HesapTuru).HasConversion<byte>();
            builder.Property(x => x.Karakter).HasConversion<byte>();
            builder.Property(x => x.ParaBirimi).HasMaxLength(3).IsFixedLength();
            builder.Property(x => x.BankaKodu).HasMaxLength(4).IsFixedLength();
            builder.Property(x => x.Iban).HasMaxLength(34);
            builder.Property(x => x.Yol).IsRequired().HasMaxLength(200);

            // Firma içinde kod benzersiz.
            builder.HasIndex(x => new { x.TenantNo, x.Kod }).IsUnique().HasDatabaseName("UQ_HesapKod");
            builder.HasIndex(x => new { x.TenantNo, x.Yol }).HasDatabaseName("IX_HesapPlani_Yol");
            builder.HasIndex(x => new { x.TenantNo, x.KodDuz }).HasDatabaseName("IX_HesapPlani_KodDuz");

            // Self-referans ağaç; hareket taşıma sonrası dahi üst hesap silinemez.
            builder.HasOne(x => x.UstHesap)
                   .WithMany(x => x.AltHesaplar)
                   .HasForeignKey(x => x.UstHesapId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
