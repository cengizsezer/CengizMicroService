using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Sovos.InvoiceWorker.Configuration;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Interfaces;
using Sovos.InvoiceWorker.Data;
using Sovos.InvoiceWorker.Services;
using Sovos.InvoiceWorker.Workers;

var builder = WebApplication.CreateBuilder(args);

// User Secrets — explicit eklenmeli (Production'da da çalışsın diye)
builder.Configuration.AddUserSecrets<Program>(optional: true);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Services.AddSerilog();

// Options
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<SovosOptions>(builder.Configuration.GetSection("Sovos"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection("Brevo"));

// EF Core
builder.Services.AddDbContext<SovosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection")));

// DataProtection — keys must be shared with SovosService.Api so encrypted
// passwords written by Worker can be decrypted by the API and vice versa.
var dpKeysFolder = builder.Configuration["DataProtection:KeysFolder"]
    ?? throw new InvalidOperationException("DataProtection:KeysFolder yapılandırması eksik.");
var dpAppName = builder.Configuration["DataProtection:ApplicationName"]
    ?? "SovosInvoiceWorker";
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysFolder))
    .SetApplicationName(dpAppName);

// Services
builder.Services.AddScoped<ICredentialProtector, CredentialProtector>();
builder.Services.AddScoped<ISovosScraper, SovosScraper>();
builder.Services.AddScoped<IInvoiceDiffService, InvoiceDiffService>();
builder.Services.AddHttpClient<IMailSender, BrevoMailSender>();
builder.Services.AddScoped<IInvoiceOrchestrator, InvoiceOrchestrator>();

// Worker (BackgroundService — saatlik tarama + günlük özet)
builder.Services.AddHostedService<InvoiceCheckWorker>();

// Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Otomatik migration uygula
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SovosDbContext>();
    db.Database.Migrate();
}

// Test modu: Sovos:TestScraperOnStartup = true iken worker başlamaz,
// appsettings'teki TestCompany ile sadece scraper çalışır.
var sovosConfig = builder.Configuration.GetSection("Sovos");
if (sovosConfig.GetValue<bool>("TestScraperOnStartup"))
{
    Log.Information("=== TEST SCRAPER MODU ===");

    using var scope = app.Services.CreateScope();
    var brevoDiag = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<BrevoOptions>>().Value;
    Log.Information(
        "Brevo Config: From={From}, ApiKeyLength={Len}",
        brevoDiag.FromAddress, brevoDiag.ApiKey?.Length ?? 0);

    var scraper = scope.ServiceProvider.GetRequiredService<ISovosScraper>();
    var diff = scope.ServiceProvider.GetRequiredService<IInvoiceDiffService>();
    var mailSender = scope.ServiceProvider.GetRequiredService<IMailSender>();
    var db = scope.ServiceProvider.GetRequiredService<SovosDbContext>();
    var protector = scope.ServiceProvider.GetRequiredService<ICredentialProtector>();
    var cfg = builder.Configuration;

    var testName = cfg["TestCompany:Name"] ?? "TestCompany";
    var password = cfg["TestCompany:Password"] ?? "";

    var company = await db.Companies.FirstOrDefaultAsync(c => c.Name == testName);
    if (company == null)
    {
        company = new Company
        {
            Name = testName,
            CompanyCode = cfg["TestCompany:CompanyCode"] ?? "",
            Username = cfg["TestCompany:Username"] ?? "",
            EncryptedPassword = protector.Encrypt(password),
            NotificationEmails = cfg["TestCompany:NotificationEmails"]
                                 ?? cfg["TestCompany:NotificationEmail"]
                                 ?? "",
            IsActive = true
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        Log.Information("Test firması DB'ye eklendi (Id={Id})", company.Id);
    }
    else
    {
        Log.Information("Test firması DB'de bulundu (Id={Id})", company.Id);
    }

    Log.Information("Test firması: {Name} / {Code} / {User}",
        company.Name, company.CompanyCode, company.Username);

    try
    {
        var scraped = await scraper.FetchPendingInvoicesAsync(
            company, password, CancellationToken.None);
        Log.Information("Scrape başarılı — {Count} onay bekleyen fatura", scraped.Count);

        var toNotify = await diff.SaveAndGetNewAsync(
            company.Id, scraped, CancellationToken.None);
        var alreadyNotified = scraped.Count - toNotify.Count;

        if (scraped.Count > 0)
        {
            var notifyKeys = toNotify
                .Select(n => (n.FaturaNo, n.GondericiVkn))
                .ToHashSet();

            Console.WriteLine();
            Console.WriteLine(
                $"{"Fatura No",-22} {"Tedarikçi",-45} {"Tutar",14} {"Birim",-5} Durum");
            Console.WriteLine(new string('-', 100));
            foreach (var inv in scraped)
            {
                var status = notifyKeys.Contains((inv.FaturaNo, inv.GondericiVkn))
                    ? "GÖNDERİLECEK" : "OK (bildirilmiş)";
                Console.WriteLine(
                    $"{inv.FaturaNo,-22} {Trunc(inv.FirmaUnvani, 45),-45} " +
                    $"{inv.FaturaTutari,14:N2} {inv.ParaBirimi,-5} {status}");
            }
            Console.WriteLine(new string('-', 100));
        }
        else
        {
            Console.WriteLine("Bu dönem için onay bekleyen fatura bulunamadı.");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Mail kuyruğu: {toNotify.Count}, zaten bildirilmiş: {alreadyNotified}");

        var mailStatus = "atlandı (mail edilecek fatura yok)";
        if (toNotify.Count > 0)
        {
            try
            {
                await mailSender.SendNewInvoicesAsync(company, toNotify, CancellationToken.None);
                await diff.MarkAsNotifiedAsync(
                    toNotify.Select(n => n.Id), CancellationToken.None);
                mailStatus = $"gönderildi → {company.NotificationEmails}";
                Console.WriteLine($"Mail {mailStatus}");
            }
            catch (Exception mailEx)
            {
                Log.Error(mailEx, "Mail gönderim hatası");
                mailStatus = $"BAŞARISIZ ({mailEx.Message})";
                Console.WriteLine($"Mail {mailStatus}");
                Console.WriteLine("  → NotifiedAt=null kaldı, sonraki turda yeniden denenecek.");
            }
        }
        else
        {
            Console.WriteLine($"Mail {mailStatus}");
        }

        Console.WriteLine(
            $"=== TEST PASSED — {scraped.Count} scraped, " +
            $"{toNotify.Count} mail edilecek, {alreadyNotified} bildirilmiş ===");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "=== TEST BAŞARISIZ ===");
        Console.WriteLine();
        Console.WriteLine("=== TEST FAILED ===");
    }

    static string Trunc(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    Log.Information("Test modu bitti. Worker başlatılmıyor. " +
        "Normal çalıştırma için appsettings'te TestScraperOnStartup=false yapın.");
    return;
}

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

await app.RunAsync();
