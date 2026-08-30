using CatalogService.Api.Extensions;
using CatalogService.Api.Features.AccountPlan;
using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Ajanlar.Services;
using CatalogService.Api.Features.Banka.Services;
using CatalogService.Api.Features.Declarations.Services;
using CatalogService.Api.Features.Education.Mapping;
using CatalogService.Api.Features.Expenses.Mapping;
using CatalogService.Api.Features.Finans.Services;
using CatalogService.Api.Features.Firmalar.Services;
using CatalogService.Api.Features.Jobs.Service;
using CatalogService.Api.Features.KdvBeyanname.Services;
using CatalogService.Api.Features.KdvBeyanname.Services.BdpXml;
using CatalogService.Api.Features.KdvBeyanname.Services.Parsing;
using CatalogService.Api.Features.Mukellefler.Services;
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

// PkfRobot ajanı: hub + bellekteki bağlı ajan listesi. Ajan dışarı doğru bağlanıyor
// (sunucu ofis makinesine uzanamıyor), liste bilerek veritabanına yazılmıyor —
// bağlantının ömrü kadar yaşıyor.
builder.Services.Configure<AgentHubAyarlari>(configuration.GetSection(AgentHubAyarlari.Bolum));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAjanDeposu, AjanDeposu>();
builder.Services.AddSingleton<IAjanIsGondericisi, HubIsGondericisi>();
builder.Services.AddScoped<IAjanIsServisi, AjanIsServisi>();
builder.Services.AddScoped<IOrkaAktarimYuku, OrkaAktarimYuku>();
builder.Services.AddSignalR();
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
builder.Services.AddScoped<CatalogService.Api.Features.PersonnelEmails.Service.IPersonnelEmailService, CatalogService.Api.Features.PersonnelEmails.Service.PersonnelEmailService>();
builder.Services.AddScoped<IHttpCurrentUser, HttpCurrentUser>();
builder.Services.AddTransient<AuthForwardingHandler>();
// Ajan ve insan token'ları aynı imzayı taşıyor; ayrımı politikalar yapıyor
// (hub'a yalnız ajan, durum ucuna yalnız insan).
builder.Services.AddAuthorization(AjanPolitikalari.Ekle);
builder.Services.AddScoped<IAccountPlanService, AccountPlanService>();
builder.Services.AddScoped<IDeclarationQueryService, DeclarationQueryService>();
builder.Services.AddScoped<IDeclarationCommandService, DeclarationCommandService>();
builder.Services.AddScoped<ICustomerCompanyQueryService, CustomerCompanyQueryService>();

// Beyanname özeti (firma × tür matrisi) ve beyanname belgeleri.
builder.Services.AddScoped<CatalogService.Api.Features.Declarations.Services.IBeyannameOzetService,
                           CatalogService.Api.Features.Declarations.Services.BeyannameOzetService>();
builder.Services.AddScoped<CatalogService.Api.Features.Declarations.Services.IBeyannameEkService,
                           CatalogService.Api.Features.Declarations.Services.BeyannameEkService>();

// Beyanname türü tanımları: Takip ve Özet'in ortak kaynağı; Tanımlar ekranından yönetilir.
builder.Services.AddScoped<CatalogService.Api.Features.Declarations.Services.IBeyannameTuruService,
                           CatalogService.Api.Features.Declarations.Services.BeyannameTuruService>();

// Firma Bilgileri (sicil / ortaklık / imza yetkilileri / belgeler). Kapsam Banka
// Otomasyon'daki mekanizmanın aynısı: ?firmaId= → BankaFirmaFiltresi → IBankaFirmaKapsami.
builder.Services.AddScoped<CatalogService.Api.Features.FirmaBilgileri.Services.IFirmaBilgiService,
                           CatalogService.Api.Features.FirmaBilgileri.Services.FirmaBilgiService>();

// Anasayfa: mevcut servislerin sayaçlarını tek çağrıda toplar.
builder.Services.AddScoped<CatalogService.Api.Features.Anasayfa.Services.IAnasayfaService,
                           CatalogService.Api.Features.Anasayfa.Services.AnasayfaService>();

// Anasayfa firma paneli: tüm firmaların künyesi + uyarıları tek çağrıda.
builder.Services.AddScoped<CatalogService.Api.Features.Anasayfa.Services.IFirmaPaneliService,
                           CatalogService.Api.Features.Anasayfa.Services.FirmaPaneliService>();
builder.Services.AddScoped<IFirmaService, FirmaService>();
builder.Services.AddScoped<IKdvBeyannameQueryService, KdvBeyannameQueryService>();
builder.Services.AddScoped<IDuzenleyenService, DuzenleyenService>();
builder.Services.AddScoped<IKdvUploadService, KdvUploadService>();
builder.Services.AddScoped<IKarsilastirmaService, KarsilastirmaService>();
builder.Services.AddScoped<IKdvSonucService, KdvSonucService>();
builder.Services.AddSingleton<IBdpXmlMapper, BdpXmlMapper>();
builder.Services.AddSingleton<IBdpXmlBuilder, BdpXmlBuilder>();
builder.Services.AddSingleton<IKdvMizanExcelParser, KdvMizanExcelParser>();
builder.Services.AddSingleton<IKdvYevmiyeExcelParser, KdvYevmiyeExcelParser>();
builder.Services.AddHttpClient("incoming-invoice-worker");
builder.Services.AddScoped<IMukellefService, MukellefService>();
builder.Services.AddScoped<IMukellefImportService, MukellefImportService>();
builder.Services.AddScoped<IFinansService, FinansService>();
builder.Services.AddScoped<IHesapService, HesapService>();
builder.Services.AddScoped<IBankaTakipService, BankaTakipService>();
builder.Services.AddScoped<IHesapNotService, HesapNotService>();
builder.Services.AddScoped<CatalogService.Api.Features.TicaretSicil.Services.ITicaretSicilService, CatalogService.Api.Features.TicaretSicil.Services.TicaretSicilService>();
builder.Services.AddScoped<CatalogService.Api.Features.MevzuatNotlari.Services.IMevzuatNotuService, CatalogService.Api.Features.MevzuatNotlari.Services.MevzuatNotuService>();
builder.Services.AddScoped<CatalogService.Api.Features.SmmmTakip.Services.ISmmmTakipService, CatalogService.Api.Features.SmmmTakip.Services.SmmmTakipService>();
builder.Services.AddScoped<CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services.IFinansmanGiderKisitlamasiService, CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services.FinansmanGiderKisitlamasiService>();
// Tekdüzen plan şablonu: dosyadan okunur, yükleme başına bir kez çözülür.
builder.Services.AddSingleton<CatalogService.Api.Features.Muhasebe.Services.ITekDuzenPlanKaynagi, CatalogService.Api.Features.Muhasebe.Services.DosyadanTekDuzenPlanKaynagi>();
builder.Services.AddScoped<CatalogService.Api.Features.Muhasebe.Services.IHesapPlaniService, CatalogService.Api.Features.Muhasebe.Services.HesapPlaniService>();
builder.Services.AddScoped<CatalogService.Api.Features.Muhasebe.Services.IFisService, CatalogService.Api.Features.Muhasebe.Services.FisService>();
builder.Services.AddScoped<CatalogService.Api.Features.Muhasebe.Services.IRaporService, CatalogService.Api.Features.Muhasebe.Services.RaporService>();
builder.Services.AddScoped<CatalogService.Api.Features.Muhasebe.Services.IMasrafMerkeziService, CatalogService.Api.Features.Muhasebe.Services.MasrafMerkeziService>();
// Ortak referans veri, dosyadan bir kez okunup bellekte tutulur.
builder.Services.AddSingleton<CatalogService.Api.Features.Muhasebe.Services.IBankaKoduService, CatalogService.Api.Features.Muhasebe.Services.BankaKoduService>();

// Banka Ekstresi İşleme modülü.
// Firma kapsamı isteğin ?firmaId= parametresinden kurulur (tenant claim'inden DEĞİL —
// bkz. KARARLAR §68/§69). Kapsam tutucusu ve onu dolduran filtre Scoped.
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Kapsam.IBankaFirmaKapsami,
                           CatalogService.Api.Features.BankaEkstre.Kapsam.BankaFirmaKapsami>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Kapsam.BankaFirmaFiltresi>();
// Firma Id -> ad; listelerdeki firma kolonu bundan doluyor (istek başına tek okuma).
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Kapsam.IFirmaAdlari,
                           CatalogService.Api.Features.BankaEkstre.Kapsam.FirmaAdlari>();

// Parser'lar durumsuz → Singleton; yeni banka eklemek için buraya bir IEkstreParser kaydı yeter.
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.Parsing.IEkstreParser,
                              CatalogService.Api.Features.BankaEkstre.Services.Parsing.VakifbankVadesizParser>();
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.Parsing.IEkstreParser,
                              CatalogService.Api.Features.BankaEkstre.Services.Parsing.IsBankasiVadesizParser>();
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.Parsing.IEkstreParser,
                              CatalogService.Api.Features.BankaEkstre.Services.Parsing.AkbankVadesizParser>();
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.Parsing.IEkstreParser,
                              CatalogService.Api.Features.BankaEkstre.Services.Parsing.ZiraatVadesizParser>();
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.Parsing.IEkstreParserSecici,
                              CatalogService.Api.Features.BankaEkstre.Services.Parsing.EkstreParserSecici>();
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.IUnvanCikarici,
                              CatalogService.Api.Features.BankaEkstre.Services.UnvanCikarici>();
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.IAciklamaUretici,
                              CatalogService.Api.Features.BankaEkstre.Services.AciklamaUretici>();
builder.Services.AddSingleton<CatalogService.Api.Features.BankaEkstre.Services.IHesapEslestirici,
                              CatalogService.Api.Features.BankaEkstre.Services.HesapEslestirici>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IBankaHesabiService,
                           CatalogService.Api.Features.BankaEkstre.Services.BankaHesabiService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IBankaHesabiIceAktarimService,
                           CatalogService.Api.Features.BankaEkstre.Services.BankaHesabiIceAktarimService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IEkstreHesapPlaniService,
                           CatalogService.Api.Features.BankaEkstre.Services.EkstreHesapPlaniService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IHesapEslesmeService,
                           CatalogService.Api.Features.BankaEkstre.Services.HesapEslesmeService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IOgrenilenEslesmeIceAktarimService,
                           CatalogService.Api.Features.BankaEkstre.Services.OgrenilenEslesmeIceAktarimService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IEkstreService,
                           CatalogService.Api.Features.BankaEkstre.Services.EkstreService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IVergiKoduService,
                           CatalogService.Api.Features.BankaEkstre.Services.VergiKoduService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.ISabitKuralService,
                           CatalogService.Api.Features.BankaEkstre.Services.SabitKuralService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IAciklamaSablonuService,
                           CatalogService.Api.Features.BankaEkstre.Services.AciklamaSablonuService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IUnvanDeseniService,
                           CatalogService.Api.Features.BankaEkstre.Services.UnvanDeseniService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IKisiYonlendirmeService,
                           CatalogService.Api.Features.BankaEkstre.Services.KisiYonlendirmeService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IIslemKategorisiService,
                           CatalogService.Api.Features.BankaEkstre.Services.IslemKategorisiService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IFirmaOzetService,
                           CatalogService.Api.Features.BankaEkstre.Services.FirmaOzetService>();
builder.Services.AddScoped<CatalogService.Api.Features.BankaEkstre.Services.IBankaTemizlikService,
                           CatalogService.Api.Features.BankaEkstre.Services.BankaTemizlikService>();
builder.Services.AddScoped<CatalogService.Api.Features.FirmaKontrol.Services.IFirmaKontrolMaddeService, CatalogService.Api.Features.FirmaKontrol.Services.FirmaKontrolMaddeService>();
builder.Services.AddScoped<CatalogService.Api.Features.FirmaKontrol.Services.IFirmaKontrolMizanService, CatalogService.Api.Features.FirmaKontrol.Services.FirmaKontrolMizanService>();
builder.Services.AddScoped<CatalogService.Api.Features.FirmaKontrol.Services.IMizanNotuService, CatalogService.Api.Features.FirmaKontrol.Services.MizanNotuService>();
builder.Services.AddScoped<CatalogService.Api.Features.FirmaKontrol.Services.IFirmaKontrolVergiService, CatalogService.Api.Features.FirmaKontrol.Services.FirmaKontrolVergiService>();
builder.Services.AddScoped<CatalogService.Api.Features.FirmaKontrol.Services.IVergiBeyannameService, CatalogService.Api.Features.FirmaKontrol.Services.VergiBeyannameService>();
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

    // WebSocket el sıkışmasında tarayıcı Authorization başlığı gönderemiyor; SignalR
    // istemcileri token'ı ?access_token= ile taşır. Yalnız hub yolunda okunuyor —
    // sıradan API uçlarında sorgu dizesinden token kabul etmek, token'ın adres
    // çubuğunda ve erişim kayıtlarında dolaşması demek olurdu.
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) &&
                ctx.HttpContext.Request.Path.StartsWithSegments(AgentHub.Yol))
            {
                ctx.Token = token;
            }
            return Task.CompletedTask;
        }
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

            // Global seed'ler adım adım ve YALITILMIŞ çalışır: biri patlarsa sonrakiler
            // yine de çalışsın ve hangisinin düştüğü adıyla loglansın. Hepsi tek bir
            // try/catch içindeyken erken bir hata sonraki tabloları sessizce boş
            // bırakıyordu (catalog.BeyannameTurleri'nin yayında boş kalma sebebi).
            var globalSeeder = new CatalogContextSeed();

            await SeedAdimi.CalistirAsync(logger, "Payroll",
                () => PayrollSeedData.SeedAsync(ctxOnce));

            await SeedAdimi.CalistirAsync(logger, "Firmalar", async () =>
            {
                await globalSeeder.SeedFirmalarAsync(ctxOnce, envHost, logger);
                await globalSeeder.SeedMukelleflerAsync(ctxOnce, envHost, logger);
            });

            await SeedAdimi.CalistirAsync(logger, "Ticaret Sicil İşlemleri",
                () => CatalogService.Api.Features.TicaretSicil.TicaretSicilSeed.SeedAsync(ctxOnce));

            await SeedAdimi.CalistirAsync(logger, "SMMM Takip",
                () => CatalogService.Api.Features.SmmmTakip.SmmmTakipSeed.SeedAsync(ctxOnce));

            await SeedAdimi.CalistirAsync(logger, "Finansman gider kısıtlaması oranları",
                () => CatalogService.Api.Features.FinansmanGiderKisitlamasi
                          .FinansmanGiderKisitlamasiSeed.SeedAsync(ctxOnce));

            // Banka ekstresi şablon/desen/kural tabloları: banka bazlı referans, tenant'tan bağımsız.
            await SeedAdimi.CalistirAsync(logger, "Banka ekstresi yapılandırması",
                () => CatalogService.Api.Features.BankaEkstre.BankaEkstreSeed.SeedAsync(ctxOnce));

            // Beyanname türü tanımları: ülke çapında aynı, tenant'tan bağımsız. Sonuç
            // loglanır; tablo boş kalırsa log hata seviyesinde uyarır.
            await SeedAdimi.CalistirAsync(logger, "Beyanname türleri",
                () => CatalogService.Api.Features.Declarations.BeyannameTuruSeed
                          .SeedVeLoglaAsync(ctxOnce, logger));

            // Kurumlar vergisi beyanname kalemleri: katalog firmadan bağımsız, bir kez yüklenir.
            await SeedAdimi.CalistirAsync(logger, "Vergi kalemleri",
                () => CatalogService.Api.Features.FirmaKontrol.VergiKalemSeed.SeedAsync(ctxOnce, envHost));
        }

        var seeder = new CatalogContextSeed();
        var tenants = new[] { "201", "106", "108", "105", "107" ,"500" };

        // 🔑 Her tenant için sabit accessor ile AYRI bir context.
        // Tenant'lar da yalıtık: bir firmanın seed'i patlarsa sıradaki firmalar yine
        // çalışsın. (Listede olmayan firmalar için kaçış yolu ekranlardaki elle yükleme
        // düğmeleridir — KARARLAR §83/§84.)
        foreach (var t in tenants)
        {
            await SeedAdimi.CalistirAsync(logger, $"Tenant {t}", async () =>
            {
                using var ctx = new CatalogContext(options, new FixedTenantAccessor(t));
                await seeder.SeedAsync(ctx, envHost, logger, new[] { t }, force: true);
                await AccountPlanSeed.SeedAsync(ctx, logger);
                // Sonuç loglanır: şablon dosyası yayında eksikse sessizce geçilmesin (KARARLAR §84).
                var planSonucu = await CatalogService.Api.Features.Muhasebe.MuhasebeSeed.SeedAsync(ctx, envHost);
                if (planSonucu.Sonuc == CatalogService.Api.Features.Muhasebe.PlanYuklemeSonuc.SablonYok)
                    logger.LogError("Tenant {Tenant}: tekdüzen hesap planı şablonu bulunamadı; plan yüklenmedi. " +
                                    "Ekrandaki \"Tek düzen hesap planını yükle\" düğmesi de bu dosyaya bağlıdır.", t);
                else if (planSonucu.Sonuc == CatalogService.Api.Features.Muhasebe.PlanYuklemeSonuc.Yuklendi)
                    logger.LogInformation("Tenant {Tenant}: tekdüzen hesap planı yüklendi ({Adet} hesap).", t, planSonucu.Adet);
            });
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

// Ajan hub'ı. nginx bu yolu Ocelot'a değil doğrudan bu container'a bağlıyor
// (deploy/nginx-agenthub.conf): uzun ömürlü WebSocket, gateway'in timeout ve
// buffering ayarlarıyla iyi geçinmiyor.
app.MapHub<AgentHub>(AgentHub.Yol);

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
