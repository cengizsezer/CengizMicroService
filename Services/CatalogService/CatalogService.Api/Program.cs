using CatalogService.Api.Extensions;
using CatalogService.Api.Infrastructure;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

// appsettings
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/appsettings.json", optional: false)
    .AddJsonFile($"Configurations/appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// serilog
var serilogConfiguration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/serilog.json", optional: false)
    .AddJsonFile($"Configurations/serilog.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Serilog init
//Log.Logger = new LoggerConfiguration()
//    .ReadFrom.Configuration(serilogConfiguration)
//    .Enrich.FromLogContext()
//    .WriteTo.Console()
//    .CreateLogger();

//Log.Information("Starting up...");

var builder = WebApplication.CreateBuilder(args);

// Serilog
//builder.Host.UseSerilog();

// Configuration ekle
builder.Configuration.AddConfiguration(configuration);

// Service registration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<CatalogSettings>(configuration.GetSection("CatalogSettings"));
builder.Services.ConfigureDbContext(configuration);
builder.Services.ConfigureConsul(configuration);

var app = builder.Build();

// DB Migration ve Seeding
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    var db = services.GetRequiredService<CatalogContext>();
//    var envService = services.GetRequiredService<IWebHostEnvironment>();
//    var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();

//    await new CatalogContextSeed().SeedAsync(db, envService, logger);
//}
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();
    var envo = services.GetRequiredService<IWebHostEnvironment>();
    var context = services.GetRequiredService<CatalogContext>();

    try
    {
        context.Database.Migrate(); // Apply migrations
        var seeder = new CatalogContextSeed();
        await seeder.SeedAsync(context, envo, logger); // Seed data
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Veritabanı migration veya seed işlemi sırasında hata oluştu.");
        throw; // isteğe bağlı: uygulamayı başlatmayı durdurabilir
    }
}
// HTTP Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.UseAuthorization();

app.MapControllers();

// Consul registration
app.RegisterWithConsul(app.Lifetime, builder.Configuration);

app.Run();
