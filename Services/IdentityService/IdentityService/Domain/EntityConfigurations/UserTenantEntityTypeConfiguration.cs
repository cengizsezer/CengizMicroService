using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class UserTenantEntityTypeConfiguration : IEntityTypeConfiguration<UserTenant>
    {
        public void Configure(EntityTypeBuilder<UserTenant> b)
        {
            b.ToTable("UserTenants");
            b.HasKey(x => new { x.UserId, x.TenantId });

            b.HasOne(x => x.User)
             .WithMany(u => u.UserTenants)          // <-- WithMany(…) BOŞ DEĞİL
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Tenant)
             .WithMany(t => t.UserTenants)          // <-- Tenant’ta aynı isimli koleksiyon var
             .HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
