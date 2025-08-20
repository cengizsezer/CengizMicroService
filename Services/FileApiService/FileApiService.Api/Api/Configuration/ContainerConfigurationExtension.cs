namespace FileApiService.Api.Api.Configuration
{
    public static class ContainerConfigurationExtension
    {
        public static WebApplicationBuilder AddLogging(this WebApplicationBuilder builder)
        {
            builder.Logging.ClearProviders().AddConsole();
            return builder;
        }

        public static IServiceCollection AddCustomCors(this IServiceCollection services, string policyName, IConfiguration cfg)
        {
            var origins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            return services.AddCors(o => o.AddPolicy(policyName, p =>
            {
                if (origins.Length > 0) p.WithOrigins(origins);
                else p.AllowAnyOrigin();
                p.AllowAnyHeader().AllowAnyMethod();
            }));
        }
    }
}
