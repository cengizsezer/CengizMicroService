using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sovos.IncomingInvoiceWorker.Configuration;
using Sovos.IncomingInvoiceWorker.Data;
using Sovos.IncomingInvoiceWorker.Services;
using Sovos.IncomingInvoiceWorker.Services.Parsing;
using Sovos.InvoiceWorker.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Services.AddSerilog();

builder.Services.Configure<IncomingSovosOptions>(builder.Configuration.GetSection("Sovos"));

var connectionString = builder.Configuration.GetConnectionString("DatabaseConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DatabaseConnection eksik.");

// Sovos DbContext — DP credentials okumak için (SovosCompanies tablosu).
builder.Services.AddDbContext<IncomingWorkerSovosDbContext>(o => o.UseSqlServer(connectionString));

// CatalogContext — GelenFaturalar + KdvBeyannameTaramalar tabloları için.
// CatalogContext multi-tenant query filter kullanıyor; worker'da tenant
// bağlamı olmadığı için sabit (boş) accessor ile bypass ediyoruz.
builder.Services.AddSingleton<IHttpCurrentTenant, WorkerTenantAccessor>();
builder.Services.AddDbContext<CatalogContext>(o => o.UseSqlServer(connectionString));

// DataProtection — keys must be shared with SovosService.Api / Sovos.InvoiceWorker
// so encrypted passwords written by them can be decrypted here.
var dpKeysFolder = builder.Configuration["DataProtection:KeysFolder"]
    ?? throw new InvalidOperationException("DataProtection:KeysFolder yapılandırması eksik.");
var dpAppName = builder.Configuration["DataProtection:ApplicationName"]
    ?? "SovosInvoiceWorker";
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysFolder))
    .SetApplicationName(dpAppName);

builder.Services.AddScoped<ICredentialProtector, CredentialProtector>();
builder.Services.AddSingleton<IGelenFaturaExcelParser, GelenFaturaExcelParser>();
builder.Services.AddScoped<IIncomingInvoiceScraper, IncomingInvoiceScraper>();
builder.Services.AddScoped<IGelenFaturaUpsertService, GelenFaturaUpsertService>();
builder.Services.AddScoped<IIncomingInvoiceOrchestrator, IncomingInvoiceOrchestrator>();

// Tek fatura PDF çekimi: firma bazlı kilit (singleton) + PDF servisi + FileApi HttpClient.
builder.Services.AddSingleton<IFirmaLock, FirmaLock>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddHttpClient("file-api");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

await app.RunAsync();

// Worker bağlamında HTTP istek yok; tenant filter'ı bypass için sabit (boş) accessor.
public class WorkerTenantAccessor : IHttpCurrentTenant
{
    public string CurrentTenantNo => string.Empty;
}
