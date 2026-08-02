using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FisEntityTypeConfiguration : IEntityTypeConfiguration<Fis>
    {
        public void Configure(EntityTypeBuilder<Fis> builder)
        {
            builder.ToTable("Fisler", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(50);
            builder.Property(x => x.FisNo).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Tarih).HasColumnType("date");
            builder.Property(x => x.FisTuru).HasConversion<byte>();
            builder.Property(x => x.Kaynak).HasConversion<byte>();
            builder.Property(x => x.Durum).HasConversion<byte>();
            builder.Property(x => x.BelgeNo).HasMaxLength(50);
            builder.Property(x => x.Aciklama).HasMaxLength(250);

            // Firma + dönem içinde fiş numarası benzersiz.
            builder.HasIndex(x => new { x.TenantNo, x.DonemYil, x.FisNo }).IsUnique().HasDatabaseName("UQ_FisNo");

            builder.HasMany(x => x.Satirlar)
                   .WithOne(x => x.Fis)
                   .HasForeignKey(x => x.FisId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
