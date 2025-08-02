using IdentityService.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace IdentityService.Application.DbRegistration
{
    public static class DbContextRegistration
    {
        public static IServiceCollection ConfigureDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<IdentityDbContext>(options =>
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
