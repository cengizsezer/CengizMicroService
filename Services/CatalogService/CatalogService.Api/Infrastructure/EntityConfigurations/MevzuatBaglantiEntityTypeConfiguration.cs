using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MevzuatBaglantiEntityTypeConfiguration : IEntityTypeConfiguration<MevzuatBaglanti>
    {
        public void Configure(EntityTypeBuilder<MevzuatBaglanti> builder)
        {
            builder.ToTable("MevzuatBaglantilar", CatalogContext.DEFAULT_SCHEMA, t =>
                t.HasCheckConstraint("CK_MevzuatBaglantilar_KendineDegil", "[KaynakKartId] <> [HedefKartId]"));

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.KaynakKartId, x.HedefKartId }).IsUnique();
            builder.HasIndex(x => x.HedefKartId);

            // Aynı tabloya iki FK: SQL Server çift cascade yolunu kabul etmez. Restrict;
            // kart silinince temizliği MevzuatGrafigiService.DeleteKartAsync yapar.
            builder.HasOne<MevzuatKart>()
                   .WithMany()
                   .HasForeignKey(x => x.KaynakKartId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<MevzuatKart>()
                   .WithMany()
                   .HasForeignKey(x => x.HedefKartId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
