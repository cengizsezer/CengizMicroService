using IdentityService.Domain.Entities;
using IdentityService.Domain.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace IdentityService.Persistence
{
    public class IdentityDbContext : DbContext
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfiguration(new UserEntityTypeConfiguration());
        }
    }


}
