using IdentityService.Domain.Entities;
using IdentityService.Domain.EntityConfigurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Security;

namespace IdentityService.Persistence
{
    public class IdentityDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
     
        public DbSet<User> Users { get; set; }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Role> RolesApp => Set<Domain.Entities.Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<UserTenant> UserTenants => Set<UserTenant>();
        public DbSet<UserTenantRole> UserTenantRoles => Set<UserTenantRole>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfiguration(new TenantEntityTypeConfiguration());
            builder.ApplyConfiguration(new RoleEntityTypeConfiguration());
            builder.ApplyConfiguration(new PermissionEntityTypeConfiguration());
            builder.ApplyConfiguration(new RolePermissionEntityTypeConfiguration());
            builder.ApplyConfiguration(new UserTenantEntityTypeConfiguration());
            builder.ApplyConfiguration(new UserTenantRoleEntityTypeConfiguration());
            builder.ApplyConfiguration(new RefreshTokenEntityTypeConfiguration());
           

            builder.Entity<Permission>().HasData(
     new Permission { Id = 1, Key = "Expense.View" },
     new Permission { Id = 2, Key = "Expense.Edit" },
     new Permission { Id = 3, Key = "Beyanname.View" },
     new Permission { Id = 4, Key = "Ilkyardim.View" }
 );
        }
    }
}
