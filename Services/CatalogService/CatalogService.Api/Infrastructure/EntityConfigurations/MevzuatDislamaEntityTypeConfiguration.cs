using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MevzuatDislamaEntityTypeConfiguration : IEntityTypeConfiguration<MevzuatDislama>
    {
        public void Configure(EntityTypeBuilder<MevzuatDislama> builder)
        {
            // Yönsüz çift sıralı saklanır: A < B. Böylece A-B ile B-A aynı unique satıra düşer.
            builder.ToTable("MevzuatDislamalar", CatalogContext.DEFAULT_SCHEMA, t =>
                t.HasCheckConstraint("CK_MevzuatDislamalar_Sirali", "[AKartId] < [BKartId]"));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Not)
                   .HasMaxLength(300);

            builder.HasIndex(x => new { x.AKartId, x.BKartId }).IsUnique();
            builder.HasIndex(x => x.BKartId);

            // Aynı tabloya iki FK: Restrict; temizlik serviste (bkz. MevzuatBaglanti).
            builder.HasOne<MevzuatKart>()
                   .WithMany()
                   .HasForeignKey(x => x.AKartId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MevzuatKart>()
                   .WithMany()
                   .HasForeignKey(x => x.BKartId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
