using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Persistence
{
    public class IdentityDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public DbSet<Firm> Firms { get; set; }
        public DbSet<UserFirm> UserFirms { get; set; }
        public DbSet<User> Users { get; set; }


        public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserFirm>()
                .HasKey(uf => new { uf.UserId, uf.FirmaId });

            builder.Entity<UserFirm>()
                .HasOne(uf => uf.User)
                .WithMany(u => u.UserFirmalar)
                .HasForeignKey(uf => uf.UserId);

            builder.Entity<UserFirm>()
                .HasOne(uf => uf.Firma)
                .WithMany(f => f.UserFirmalar)
                .HasForeignKey(uf => uf.FirmaId);
        }
    }
}
