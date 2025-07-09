using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IdentityService.Persistence
{
    public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
    {
        public IdentityDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
            optionsBuilder.UseSqlServer("Server=DESKTOP-I290HP6;Initial Catalog=UserDataBase;Integrated Security=True;TrustServerCertificate=True;");
            return new IdentityDbContext(optionsBuilder.Options);
        }
    }

}
