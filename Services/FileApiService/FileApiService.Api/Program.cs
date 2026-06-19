using FileApiService.Api.Api.Configuration;
using FileApiService.Api.Api.EndpointBuilders;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Commands;
using FileApiService.Api.Core.Extensions;
using FileApiService.Api.Core.Queries;
using FileApiService.Api.Core.Validation;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Options;
using FileApiService.Api.Domain.Queries;
using FileApiService.Api.Infrastructure.Minio;
using FileApiService.Api.Infrastructure.Persistence.Entities;
using FileApiService.Api.Infrastructure.Persistence.Repositories;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Minio;
using Serilog;
using Validot;

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

if (env == "Docker")
{
    builder.WebHost.UseUrls("http://0.0.0.0:5009"); // container dışına açıl
}
else
{
    builder.WebHost.UseUrls("http://localhost:5009"); // local dev için
}
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

builder.Services.ConfigureConsul(configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Options
builder.Services.Configure<FilesOptions>(builder.Configuration.GetSection(FilesOptions.Files));
builder.Services.ConfigureDbContext(configuration);

// Repos
builder.Services.AddScoped<IFileCommandsRepository, FileCommandsRepository>();
builder.Services.AddScoped<IFileQueriesRepository, FileQueriesRepository>();
builder.Services.AddScoped<IDeleteFileCommandHandler, DeleteFileCommandHandler>();
// MinIO
//var endpoint = builder.Configuration["Minio:Endpoint"] ?? "localhost:9000";
//var access = builder.Configuration["Minio:AccessKey"] ?? "admin";
//var secret = builder.Configuration["Minio:SecretKey"] ?? "admin12345";
//var withSSL = bool.TryParse(builder.Configuration["Minio:WithSSL"], out var ssl) && ssl;
//var bucket = builder.Configuration["Minio:Bucket"] ?? "dijitalmasraf";

//var minio = new MinioClient().WithEndpoint(endpoint).WithCredentials(access, secret).WithSSL(withSSL).Build();
//builder.Services.AddSingleton<IMinioClient>(minio);
//builder.Services.AddScoped<IFileStorage>(_ => new MinioFileStorage(minio, bucket));
//builder.Services.AddScoped<IPresignUrlService>(_ => new MinioPresignUrlService(minio, bucket));
var endpoint = builder.Configuration["Minio:Endpoint"] ?? "s_minio:9000";
var access = builder.Configuration["Minio:AccessKey"] ?? "admin";
var secret = builder.Configuration["Minio:SecretKey"] ?? "admin12345";
var withSSL = bool.TryParse(builder.Configuration["Minio:WithSSL"], out var ssl) && ssl;
var bucket = builder.Configuration["Minio:Bucket"] ?? "dijitalmasraf";

var internalClient = new MinioClient()
  .WithEndpoint(endpoint)
  .WithCredentials(access, secret)
  .WithSSL(withSSL)
  .Build();

builder.Services.AddSingleton<IMinioClient>(internalClient);
builder.Services.AddScoped<IFileStorage>(_ => new MinioFileStorage(internalClient, bucket));

// Sadece prod’da env veriyoruz → lokal bunu görmez, etkilenmez
var publicHost = builder.Configuration["MinioPublic:Endpoint"];
if (!string.IsNullOrWhiteSpace(publicHost))
{
    var publicClient = new MinioClient()
      .WithEndpoint(publicHost)    // "s3.dijitalmasraf.com"
      .WithCredentials(access, secret)
      .WithSSL(true)
      .Build();

    builder.Services.AddScoped<IPresignUrlService>(_ => new MinioPresignUrlService(publicClient, bucket));
}
else
{
    builder.Services.AddScoped<IPresignUrlService>(_ => new MinioPresignUrlService(internalClient, bucket));
}

// Handlers
builder.Services.AddScoped<IAddFilesCommandHandler, AddFilesCommandHandler>();
builder.Services.AddScoped<IGetFilesInfoQueryHandler, GetFilesInfoQueryHandler>();
builder.Services.AddScoped<IDownloadFileQueryHandler, DownloadFileQueryHandler>();
builder.Services.AddScoped<IFileByOptionsValidator, FileByOptionsValidator>();
builder.Services.AddScoped<IAddCompanyDocCommandHandler, AddCompanyDocCommandHandler>();
builder.Services.AddScoped<IAddGenericFileCommandHandler, AddGenericFileCommandHandler>();
builder.Services.AddScoped<IGetCompanyDocsInfoQueryHandler, GetCompanyDocsInfoQueryHandler>();

// Validators (Validot + custom)
builder.Services.AddScoped<IAddFilesCommandValidator, AddFilesCommandValidator>();
builder.Services.AddScoped<IAddCompanyDocCommandValidator, AddCompanyDocCommandValidator>();
builder.Services.AddValidotSingleton<DownloadFileQuerySpecificationHolder, DownloadFileQuery>();
builder.Services.AddValidotSingleton<AddFileCommandSpecificationHolder, AddFilesCommand>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 20 * 1024 * 1024; // 20 MB
});
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 20 * 1024 * 1024;
});

// Swagger + CORS + Controllers

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var m = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var bucketName = app.Configuration["Minio:Bucket"] ?? "dijitalmasraf";

    try
    {
        var exists = await m.BucketExistsAsync(
            new Minio.DataModel.Args.BucketExistsArgs().WithBucket(bucketName));

        if (!exists)
            await m.MakeBucketAsync(
                new Minio.DataModel.Args.MakeBucketArgs().WithBucket(bucketName));
    }
    catch (Exception ex)
    {
        // logla ama uygulamayı öldürme (MinIO geç gelirse)
        Serilog.Log.Warning(ex, "MinIO bucket kontrolü başarısız: {Bucket}", bucketName);
    }
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FileDbContext>();
    // InMemory değilse migrate et
    if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        await db.Database.MigrateAsync();
}
// Middleware

app.UseAuthorization();

// Routing
app.UseCors("AllowAll");
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