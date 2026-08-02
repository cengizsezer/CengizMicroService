using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CatalogService.Api.Infrastructure.EntityConfigurations
{
    public class MasrafMerkeziEntityTypeConfiguration : IEntityTypeConfiguration<MasrafMerkezi>
    {
        public void Configure(EntityTypeBuilder<MasrafMerkezi> builder)
        {
            builder.ToTable("MasrafMerkezleri", CatalogContext.DEFAULT_SCHEMA);

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TenantNo).IsRequired().HasMaxLength(50);
            builder.Property(x => x.Kod).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Ad).IsRequired().HasMaxLength(100);

            builder.HasIndex(x => new { x.TenantNo, x.Kod }).IsUnique();
        }
    }
}
