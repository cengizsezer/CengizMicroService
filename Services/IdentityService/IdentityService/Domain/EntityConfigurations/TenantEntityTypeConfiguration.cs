using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class TenantEntityTypeConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> b)
        {
            b.ToTable("Tenants");
            b.HasIndex(x => x.FirmaNo).IsUnique();
            b.Property(x => x.FirmaNo).HasMaxLength(32).IsRequired();
            b.Property(x => x.Ad).HasMaxLength(256).IsRequired();
            b.Property(x => x.Vkn).HasMaxLength(16);
        }
    }
}
