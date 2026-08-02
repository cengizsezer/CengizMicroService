using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class KodMaskesiEntityTypeConfiguration : IEntityTypeConfiguration<KodMaskesi>
    {
        public void Configure(EntityTypeBuilder<KodMaskesi> builder)
        {
            builder.ToTable("KodMaskeleri", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(50);
            builder.Property(x => x.SegmentUzunluk).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Ayrac).IsRequired().HasMaxLength(1).IsFixedLength();

            // Firma başına tek maske.
            builder.HasIndex(x => x.TenantNo).IsUnique();
        }
    }
}
