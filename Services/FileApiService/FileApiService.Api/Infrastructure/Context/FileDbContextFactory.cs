using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FileApiService.Api.Infrastructure.Persistence;

public class FileDbContextFactory : IDesignTimeDbContextFactory<FileDbContext>
{
    public FileDbContext CreateDbContext(string[] args)
    {
        // env yoksa Development'a düş
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Configurations/appsettings.json", optional: false)
            .AddJsonFile($"Configurations/appsettings.{env}.json", optional: true)
            .Build();

        var cs = cfg.GetConnectionString("DatabaseConnection")
                 ?? throw new InvalidOperationException("ConnectionStrings:DatabaseConnection not set.");

        var options = new DbContextOptionsBuilder<FileDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new FileDbContext(options);
    }
}
