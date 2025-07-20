using CatalogService.Api.Extensions;
using CatalogService.Api.Infrastructure;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Serilog;

Console.WriteLine("🔧 Program.cs başlatılıyor...");

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
Console.WriteLine($"🌍 Ortam: {env}");

// appsettings
Console.WriteLine("📁 Configurations dosyaları yükleniyor...");
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/appsettings.json", optional: false)
    .AddJsonFile($"Configurations/appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
Console.WriteLine("✅ Configurations yüklendi.");

// serilog
Console.WriteLine("📁 Serilog konfigürasyonu yükleniyor...");
var serilogConfiguration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/serilog.json", optional: false)
    .AddJsonFile($"Configurations/serilog.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
Console.WriteLine("✅ Serilog ayarları yüklendi.");

// Serilog init (Yorumda)
Console.WriteLine("📝 Logger (Serilog) atlandı.");

// builder başlat
Console.WriteLine("🔧 WebApplication Builder oluşturuluyor...");
var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("✅ Builder hazır.");

// Serilog (Yorumda)
Console.WriteLine("📝 builder.Host.UseSerilog() atlandı.");

// Configuration ekle
builder.Configuration.AddConfiguration(configuration);
Console.WriteLine("✅ Ek Configuration eklendi.");

// Service registration
Console.WriteLine("🔧 Servisler ekleniyor...");
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<CatalogSettings>(configuration.GetSection("CatalogSettings"));
builder.Services.ConfigureDbContext(configuration);
builder.Services.ConfigureConsul(configuration);
Console.WriteLine("✅ Servisler başarıyla eklendi.");

// App build
Console.WriteLine("🏗️ Uygulama inşa ediliyor...");
var app = builder.Build();
Console.WriteLine("✅ App build tamamlandı.");

// DB Migration & Seeding
Console.WriteLine("🔄 Veritabanı migration ve seeding işlemi başlıyor...");
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();
    var envo = services.GetRequiredService<IWebHostEnvironment>();
    var context = services.GetRequiredService<CatalogContext>();

    try
    {
        Console.WriteLine("📦 Migration uygulanıyor...");
        context.Database.Migrate(); // Apply migrations
        Console.WriteLine("✅ Migration tamamlandı.");

        Console.WriteLine("🌱 Seed işlemi başlıyor...");
        var seeder = new CatalogContextSeed();
        await seeder.SeedAsync(context, envo, logger); // Seed data
        Console.WriteLine("✅ Seed işlemi tamamlandı.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Migration/Seeding Hatası: " + ex.Message);
        Console.WriteLine(ex.StackTrace);
        //logger.LogError(ex, "Veritabanı migration veya seed işlemi sırasında hata oluştu.");
        Console.WriteLine("❌ Migration veya Seed hatası: " + ex.Message);
        //throw;
    }
}

// Swagger
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("🧪 Development ortamı, Swagger açılıyor...");
    app.UseSwagger();
    app.UseSwaggerUI();
    Console.WriteLine("✅ Swagger aktif.");
}

// Middleware
Console.WriteLine("➡️ HTTPS yönlendirme ve authorization middleware ekleniyor...");
app.UseHttpsRedirection();
app.UseAuthorization();
Console.WriteLine("✅ Middleware tamamlandı.");

// Controller routing
Console.WriteLine("🗺️ Controller'lar map ediliyor...");
app.MapControllers();
Console.WriteLine("✅ Controller mapping tamamlandı.");

// Consul kaydı
Console.WriteLine("📡 Consul kaydı başlatılıyor...");
app.RegisterWithConsul(app.Lifetime, builder.Configuration);
Console.WriteLine("✅ Consul kaydı tamamlandı.");

// Bağlantı test
Console.WriteLine("🔌 SQL bağlantısı test ediliyor...");
try
{
    using var con = new SqlConnection(configuration.GetConnectionString("DatabaseConnection"));
    con.Open();
    Console.WriteLine("✅ SQL bağlantısı başarılı");
}
catch (Exception ex)
{
    Console.WriteLine("❌ SQL bağlantısı başarısız: " + ex.Message);
}

// Uygulama çalıştırılıyor
Console.WriteLine("🚀 Uygulama başlatılıyor...");
app.Run();
