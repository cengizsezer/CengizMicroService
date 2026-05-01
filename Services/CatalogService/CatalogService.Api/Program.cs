using CatalogService.Api.Extensions;
using CatalogService.Api.Features.AccountPlan;
using CatalogService.Api.Features.Declarations.Services;
using CatalogService.Api.Features.Education.Mapping;
using CatalogService.Api.Features.Expenses.Mapping;
using CatalogService.Api.Features.Jobs.Service;
using CatalogService.Api.Features.Payroll.Persistence.Seeds;
using CatalogService.Api.Features.Payroll.Services;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.TaxPayments.Service;
using CatalogService.Api.Features.Vehicles.Mapping;
using CatalogService.Api.Features.Vehicles.Service;
using CatalogService.Api.Infrastructure;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Seeding;
using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Factory;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using Serilog;
using System.Reflection;
using System.Text;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(serilogConfiguration)
    .CreateLogger();
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.Configuration.AddConfiguration(configuration);
builder.Services.AddScoped<VehicleService>(); // domain servisin
builder.Services.AddScoped<TaxPaymentService>(); // domain servisin

if (env == "Docker")
{
    builder.WebHost.UseUrls("http://0.0.0.0:5004"); // container dışına açıl
}
else
{
    builder.WebHost.UseUrls("http://localhost:5004"); // local dev için
}


// Service Registration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<CatalogSettings>(configuration.GetSection("CatalogSettings"));
builder.Services.ConfigureDbContext(configuration);
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
builder.Services.ConfigureConsul(configuration);
builder.Services.AddAutoMapper(typeof(ExpenseProfile));
builder.Services.AddAutoMapper(typeof(VehicleProfile));
builder.Services.AddAutoMapper(typeof(EducationProfile));

builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});


builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IHttpCurrentTenant, HttpCurrentTenant>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IHttpCurrentUser, HttpCurrentUser>();
builder.Services.AddTransient<AuthForwardingHandler>();
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAccountPlanService, AccountPlanService>();
builder.Services.AddScoped<IDeclarationQueryService, DeclarationQueryService>();
builder.Services.AddScoped<IDeclarationCommandService, DeclarationCommandService>();
builder.Services.AddScoped<ICustomerCompanyQueryService, CustomerCompanyQueryService>();
builder.Services.AddScoped<IPayrollCalculationEngine, PayrollCalculationEngine>();
builder.Services.AddScoped<IDistributionComparisonService, DistributionComparisonService>();
builder.Services.AddScoped<IDistributionExportService, DistributionExportService>();
builder.Services.AddScoped<IPayrollCalculationExportService, PayrollCalculationExportService>();

builder.Services.AddSingleton<IEventBus>(sp =>
{
    var cfg = builder.Configuration;
    var factory = new ConnectionFactory
    {
        HostName = cfg["RabbitMQ:HostName"] ?? "localhost",
        Port = int.TryParse(cfg["RabbitMQ:Port"], out var p) ? p : 5672,
        UserName = cfg["RabbitMQ:UserName"] ?? "guest",
        Password = cfg["RabbitMQ:Password"] ?? "guest",
        AutomaticRecoveryEnabled = true
    };

    var ebCfg = new EventBusConfig
    {
        ConnectionRetryCount = 5,
        EventNameSuffix = "IntegrationEvent",
        SubscriberClientAppName = "CatalogService",   // sadece isim
        EventBusType = EventBusType.RabbitMQ,
        Connection = factory
    };

    return EventBusFactory.Create(ebCfg, sp);
});

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("wasm", p => p
        .WithOrigins(
            "http://localhost:2000", "https://localhost:2000",
            "https://dijitalmasraf.com",
            "https://www.dijitalmasraf.com")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.RequireHttpsMetadata = false; // dev
    var jwt = builder.Configuration.GetSection("Jwt");
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwt["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
        ValidateLifetime = true
    };
});

var app = builder.Build();


if (args.Contains("--seed-force"))
{
    var runner = new SeedRunner(app.Services);
    await runner.RunAsync(SeedMode.PayrollOnly);
    return;
}

//if (args.Contains("--seed-force"))
//{
//    using var scope = app.Services.CreateScope();
//    var services = scope.ServiceProvider;
//    var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();
//    var envHost = services.GetRequiredService<IWebHostEnvironment>();
//    var options = services.GetRequiredService<DbContextOptions<CatalogContext>>();
//    try
//    {
//        // Şema + migration tek seferlik, tenant bağımsız bir context ile
//        //using (var ctxOnce = new CatalogContext(options, new FixedTenantAccessor("schema")))
//        //{
//        //    ctxOnce.Database.ExecuteSqlRaw(
//        //        "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalog') EXEC('CREATE SCHEMA catalog');");
//        //    ctxOnce.Database.Migrate();
//        //}

//        using (var ctxOnce = new CatalogContext(options, new FixedTenantAccessor("schema")))
//        {
//            logger.LogInformation("🗑️ Mevcut catalog tablo verileri temizleniyor (varsa)...");

//            var tables = new[] { "ProductDetails", "ReceiptItems", "Expenses", "AccountingCodes", "Personnels" };

//            foreach (var t in tables)
//            {
//                ctxOnce.Database.ExecuteSqlRaw($@"
//IF OBJECT_ID(N'[catalog].[{t}]','U') IS NOT NULL
//    DELETE FROM [catalog].[{t}];
//");
//            }

//            logger.LogInformation("🧱 Migration uygulanıyor...");
//            await ctxOnce.Database.MigrateAsync();

//            logger.LogInformation("💼 Payroll seed uygulanıyor...");
//            await PayrollSeedData.SeedAsync(ctxOnce);
//        }

//        var seeder = new CatalogContextSeed();
//        var tenants = new[] { "201", "106", "108", "105", "107","500" };

//        // 🔑 Her tenant için sabit accessor ile AYRI bir context
//        foreach (var t in tenants)
//        {
//            using var ctx = new CatalogContext(options, new FixedTenantAccessor(t));
//            await seeder.SeedAsync(ctx, envHost, logger, new[] { t }, force: true);
//        }
//    }
//    catch (Exception ex)
//    {
//        logger.LogError(ex, "❌ Seed-force sırasında hata oluştu.");
//    }

//    return;
//    //try
//    //{
//    //    logger.LogInformation("🧨 Seed-force başlatıldı: veritabanı yeniden oluşturulacak...");

//    //    // ❗ Eski verileri ve şemayı komple silmek istiyorsan (dev ortamı için uygundur)
//    //    await context.Database.EnsureDeletedAsync();
//    //    logger.LogInformation("🗑️ Eski veritabanı silindi.");

//    //    // ❗ Veritabanını yeniden oluştur ve migration'ı uygula
//    //    await context.Database.MigrateAsync();
//    //    logger.LogInformation("🧱 Migration tamamlandı, veritabanı yeniden oluşturuldu.");

//    //    // ✅ Seed işlemi
//    //    var seeder = new CatalogContextSeed();
//    //    await seeder.SeedAsync(context, envHost, logger, force: true);
//    //    logger.LogInformation("✅ Veritabanı seed işlemi tamamlandı (sadece seed).");
//    //}
//    //catch (Exception ex)
//    //{
//    //    logger.LogError(ex, "❌ Seed-force sırasında hata oluştu.");
//    //}

//    //return;
//}

//if (args.Contains("--seed-force"))
//{
//    using var scope = app.Services.CreateScope();
//    var services = scope.ServiceProvider;
//    var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();
//    var envHost = services.GetRequiredService<IWebHostEnvironment>();
//    var options = services.GetRequiredService<DbContextOptions<CatalogContext>>();

//    var seedSucceeded = false;
//    string? seedErrorMessage = null;
//    Exception? seedException = null;

//    try
//    {
//        logger.LogInformation("========================================");
//        logger.LogInformation("🚀 SEED-FORCE BAŞLADI");
//        logger.LogInformation("Environment: {Environment}", envHost.EnvironmentName);
//        logger.LogInformation("========================================");

//        using (var ctxOnce = new CatalogContext(options, new FixedTenantAccessor("schema")))
//        {
//            logger.LogInformation("🧹 Catalog verileri temizleniyor ve şema kontrol ediliyor...");

//            ctxOnce.Database.ExecuteSqlRaw(@"
//IF OBJECT_ID(N'[catalog].[ProductDetails]', 'U') IS NOT NULL
//    DELETE FROM [catalog].[ProductDetails];

//IF OBJECT_ID(N'[catalog].[ReceiptItems]', 'U') IS NOT NULL
//    DELETE FROM [catalog].[ReceiptItems];

//IF OBJECT_ID(N'[catalog].[Expenses]', 'U') IS NOT NULL
//    DELETE FROM [catalog].[Expenses];

//IF OBJECT_ID(N'[catalog].[AccountingCodes]', 'U') IS NOT NULL
//    DELETE FROM [catalog].[AccountingCodes];

//IF OBJECT_ID(N'[catalog].[Personnels]', 'U') IS NOT NULL
//    DELETE FROM [catalog].[Personnels];

//IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'pkf')
//    EXEC('CREATE SCHEMA pkf');
//");

//            logger.LogInformation("🧱 Migration uygulanıyor...");
//            await ctxOnce.Database.MigrateAsync();

//            logger.LogInformation("💼 Payroll seed uygulanıyor...");
//            await PayrollSeedData.SeedAsync(ctxOnce);
//        }

//        var seeder = new CatalogContextSeed();
//        var tenants = new[] { "201", "106", "108", "105", "107", "500" };

//        foreach (var t in tenants)
//        {
//            logger.LogInformation("🏢 Tenant seed başlıyor: {Tenant}", t);

//            using var ctx = new CatalogContext(options, new FixedTenantAccessor(t));
//            await seeder.SeedAsync(ctx, envHost, logger, new[] { t }, force: true);
//            await AccountPlanSeed.SeedAsync(ctx, logger);

//            logger.LogInformation("✅ Tenant seed tamamlandı: {Tenant}", t);
//        }

//        seedSucceeded = true;
//    }
//    catch (Exception ex)
//    {
//        seedSucceeded = false;
//        seedException = ex;
//        seedErrorMessage = ex.Message;

//        logger.LogError(ex, "❌ Seed-force sırasında hata oluştu.");
//    }
//    finally
//    {
//        logger.LogInformation("========================================");

//        if (seedSucceeded)
//        {
//            logger.LogInformation("✅ SEED-FORCE BAŞARILI TAMAMLANDI");
//        }
//        else
//        {
//            logger.LogWarning("⚠️ SEED-FORCE HATA İLE TAMAMLANDI");
//            logger.LogWarning("Hata Özeti: {ErrorMessage}", seedErrorMessage);

//            if (seedException is Microsoft.Data.SqlClient.SqlException sqlEx)
//            {
//                logger.LogWarning("SQL Error Number: {ErrorNumber}", sqlEx.Number);
//                logger.LogWarning("SQL Error State : {ErrorState}", sqlEx.State);
//                logger.LogWarning("SQL Error Class : {ErrorClass}", sqlEx.Class);
//            }

//            logger.LogWarning("Not: Uygulama Jenkins pipeline'ını kırmızıya düşürmeden Exit 0 ile kapanacaktır.");
//        }

//        logger.LogInformation("========================================");
//    }

//    return;
//}
// Migration & Seed
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();
//    var envHost = services.GetRequiredService<IWebHostEnvironment>();
//    var context = services.GetRequiredService<CatalogContext>();

//    try
//    {
//        context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalog') EXEC('CREATE SCHEMA catalog');");
//        context.Database.Migrate();
//        var seeder = new CatalogContextSeed();
//        var tenants = new[] { "201", "106", "108", "105", "107" };
//        await seeder.SeedAsync(context, envHost, logger, tenants, force: true);
//    }
//    catch (Exception ex)
//    {
//        logger.LogError(ex, "Migration veya Seed sırasında hata oluştu.");
//    }
//}

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<CatalogContextSeed>>();
    var envHost = sp.GetRequiredService<IWebHostEnvironment>();
    var options = sp.GetRequiredService<DbContextOptions<CatalogContext>>();

    try
    {
        // Şema + migration tek seferlik, tenant bağımsız bir context ile
        //using (var ctxOnce = new CatalogContext(options, new FixedTenantAccessor("schema")))
        //{
        //    ctxOnce.Database.ExecuteSqlRaw(
        //        "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalog') EXEC('CREATE SCHEMA catalog');");
        //    ctxOnce.Database.Migrate();
        //}

//        using (var ctxOnce = new CatalogContext(options, new FixedTenantAccessor("schema")))
//        {
//            logger.LogInformation("🗑️Catalog verileri temizleniyor ve şema kontrol ediliyor...");
//            //await ctxOnce.Database.EnsureDeletedAsync(); // tüm veritabanını siler
//            ctxOnce.Database.ExecuteSqlRaw("DELETE FROM [catalog].[ProductDetails]");
//            ctxOnce.Database.ExecuteSqlRaw("DELETE FROM [catalog].[ReceiptItems]");
//            ctxOnce.Database.ExecuteSqlRaw("DELETE FROM [catalog].[Expenses]");
//            ctxOnce.Database.ExecuteSqlRaw("DELETE FROM [catalog].[AccountingCodes]");
//            ctxOnce.Database.ExecuteSqlRaw("DELETE FROM [catalog].[Personnels]");
//            ctxOnce.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'pkf') EXEC('CREATE SCHEMA pkf');"
//);
//            logger.LogInformation("🧱 Migration uygulanıyor...");
//            await ctxOnce.Database.MigrateAsync(); // migration'ları sıfırdan uygular
//            await PayrollSeedData.SeedAsync(ctxOnce);

//        }

        using (var ctxOnce = new CatalogContext(options, new FixedTenantAccessor("schema")))
        {
            logger.LogInformation("🧹 Catalog verileri temizleniyor ve şema kontrol ediliyor...");

            ctxOnce.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[catalog].[ProductDetails]', 'U') IS NOT NULL
    DELETE FROM [catalog].[ProductDetails];

IF OBJECT_ID(N'[catalog].[ReceiptItems]', 'U') IS NOT NULL
    DELETE FROM [catalog].[ReceiptItems];

IF OBJECT_ID(N'[catalog].[Expenses]', 'U') IS NOT NULL
    DELETE FROM [catalog].[Expenses];

IF OBJECT_ID(N'[catalog].[AccountingCodes]', 'U') IS NOT NULL
    DELETE FROM [catalog].[AccountingCodes];

IF OBJECT_ID(N'[catalog].[Personnels]', 'U') IS NOT NULL
    DELETE FROM [catalog].[Personnels];

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'pkf')
    EXEC('CREATE SCHEMA pkf');
");

            logger.LogInformation("🧱 Migration uygulanıyor...");
            await ctxOnce.Database.MigrateAsync();

            logger.LogInformation("💼 Payroll seed uygulanıyor...");
            await PayrollSeedData.SeedAsync(ctxOnce);
        }

        var seeder = new CatalogContextSeed();
        var tenants = new[] { "201", "106", "108", "105", "107" ,"500" };

        // 🔑 Her tenant için sabit accessor ile AYRI bir context
        foreach (var t in tenants)
        {
            using var ctx = new CatalogContext(options, new FixedTenantAccessor(t));
            await seeder.SeedAsync(ctx, envHost, logger, new[] { t }, force: true);
            await AccountPlanSeed.SeedAsync(ctx, logger);

        }
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
    app.UseDeveloperExceptionPage(); // swagger'ın hemen üstü/altı fark etmez
    app.UseSwaggerUI();
}

// Middleware
app.UseHttpsRedirection();
app.UseCors("wasm");
app.UseAuthentication();
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
