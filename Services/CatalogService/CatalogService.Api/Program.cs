using CatalogService.Api.Extensions;
using CatalogService.Api.Infrastructure;
using CatalogService.Api.Infrastructure.Context;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/appsettings.json", optional: false)
    .AddJsonFile($"Configurations/appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var serilogConfiguration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/serilog.json", optional: false)
    .AddJsonFile($"Configurations/serilog.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

//Log.Logger = new LoggerConfiguration()
//    .ReadFrom.Configuration(serilogConfiguration)
//    .CreateLogger();
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.Configuration.AddConfiguration(configuration);
builder.WebHost.UseUrls("http://localhost:5004");

// Service Registration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<CatalogSettings>(configuration.GetSection("CatalogSettings"));
builder.Services.ConfigureDbContext(configuration);
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
builder.Services.ConfigureConsul(configuration);

var app = builder.Build();

// Migration & Seed
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();
    var envHost = services.GetRequiredService<IWebHostEnvironment>();
    var context = services.GetRequiredService<CatalogContext>();

    try
    {
        context.Database.Migrate();
        var seeder = new CatalogContextSeed();
        await seeder.SeedAsync(context, envHost, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration veya Seed sırasında hata oluştu.");
    }
}

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware
app.UseHttpsRedirection();
app.UseAuthorization();

// Routing
app.MapControllers();

// HealthChecks
app.MapHealthChecks("/hc", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/liveness", new HealthCheckOptions
{
    Predicate = r => r.Name.Contains("self"),
    ResponseWriter = async (context, _) => await Task.CompletedTask
});

// Consul
app.RegisterWithConsul(app.Lifetime, builder.Configuration);

// SQL Test (İsteğe Bağlı - Prod ortamda tavsiye edilmez)
try
{
    using var con = new SqlConnection(configuration.GetConnectionString("DatabaseConnection"));
    con.Open();
    Log.Information("SQL bağlantısı başarılı.");
}
catch (Exception ex)
{
    Log.Error(ex, "SQL bağlantısı başarısız.");
}

app.Run();
