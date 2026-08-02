using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class FisSatirEntityTypeConfiguration : IEntityTypeConfiguration<FisSatir>
    {
        public void Configure(EntityTypeBuilder<FisSatir> builder)
        {
            builder.ToTable("FisSatirlar", CatalogContext.DEFAULT_SCHEMA, t =>
                t.HasCheckConstraint("CK_TekTaraf",
                    "([Borc] > 0 AND [Alacak] = 0) OR ([Alacak] > 0 AND [Borc] = 0)"));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Aciklama).HasMaxLength(250);
            builder.Property(x => x.Borc).HasPrecision(19, 2);
            builder.Property(x => x.Alacak).HasPrecision(19, 2);
            builder.Property(x => x.ParaBirimi).IsRequired().HasMaxLength(3).IsFixedLength();
            builder.Property(x => x.Doviz).HasPrecision(19, 4);
            builder.Property(x => x.Kur).HasPrecision(19, 6);

            // Hesap silinemez (hareket taşıma/pasife alma ile yönetilir).
            builder.HasOne(x => x.Hesap)
                   .WithMany()
                   .HasForeignKey(x => x.HesapId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.MasrafMerkezi)
                   .WithMany()
                   .HasForeignKey(x => x.MasrafMerkeziId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.HesapId, x.FisId }).HasDatabaseName("IX_FisSatir_Hesap");
        }
    }
}
