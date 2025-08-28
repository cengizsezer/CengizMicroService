using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Domain.EntityConfigurations
{
    public class UserTenantRoleEntityTypeConfiguration : IEntityTypeConfiguration<UserTenantRole>
    {
        public void Configure(EntityTypeBuilder<UserTenantRole> b)
        {
            b.ToTable("UserTenantRoles");
            b.HasKey(x => new { x.UserId, x.TenantId, x.RoleId });

            // Composite FK -> UserTenants (CASCADE OK)
            b.HasOne<UserTenant>()
             .WithMany(ut => ut.Roles)
             .HasForeignKey(x => new { x.UserId, x.TenantId })
             .OnDelete(DeleteBehavior.Cascade);

            // Role FK -> Restrict (multiple cascade path'i engeller)
            b.HasOne(x => x.Role)
             .WithMany(r => r.Users)
             .HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.Restrict);

            // ❌ ŞUNLARI KALDIR:
            // b.HasOne<User>()...NoAction()
            // b.HasOne<Tenant>()...NoAction()
        }
    }
}
