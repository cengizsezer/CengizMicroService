using FileApiService.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace FileApiService.Api.Core.Extensions
{
    public static class DbContextRegistration
    {
        public static IServiceCollection ConfigureDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<FileDbContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("DatabaseConnection"),
                    sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(typeof(DbContextRegistration).GetTypeInfo().Assembly.GetName().Name);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 15,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null
                        );
                    });
            });

            return services;
        }
    }
}
