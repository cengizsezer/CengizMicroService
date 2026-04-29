using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sovos.InvoiceWorker.Data;

public class SovosDbContextFactory : IDesignTimeDbContextFactory<SovosDbContext>
{
    public SovosDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SovosDbContext>();
        optionsBuilder.UseSqlServer(config.GetConnectionString("Default"));

        return new SovosDbContext(optionsBuilder.Options);
    }
}
