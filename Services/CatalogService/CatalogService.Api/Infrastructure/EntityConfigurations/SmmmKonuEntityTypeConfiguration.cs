using CatalogService.Api.Features.SmmmTakip.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class SmmmKonuEntityTypeConfiguration : IEntityTypeConfiguration<SmmmKonu>
    {
        public void Configure(EntityTypeBuilder<SmmmKonu> builder)
        {
            builder.ToTable("SmmmKonular", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Baslik)
                   .IsRequired()
                   .HasMaxLength(250);

            builder.Property(x => x.Slug)
                   .IsRequired()
                   .HasMaxLength(200);

            // IcerikMd: markdown, uzunluk sınırsız (nvarchar(max)).

            builder.HasIndex(x => x.Slug).IsUnique();
            builder.HasIndex(x => x.UstKonuId);

            // Self-referans hiyerarşi: üst konu silinemez (Restrict).
            builder.HasMany(x => x.AltKonular)
                   .WithOne()
                   .HasForeignKey(x => x.UstKonuId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Konu silinince hadleri de silinir (Cascade).
            builder.HasMany(x => x.Hadler)
                   .WithOne()
                   .HasForeignKey(x => x.KonuId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
