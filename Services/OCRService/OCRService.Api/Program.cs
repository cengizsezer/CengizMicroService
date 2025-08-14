using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OCRService.Api.Application.ConsulRegistration;
using OCRService.Api.Services;
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

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(serilogConfiguration)
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddConfiguration(configuration);
builder.Host.UseSerilog();
if (env == "Docker")
{
    builder.WebHost.UseUrls("http://0.0.0.0:5002"); // container dışına açıl
}
else
{
    builder.WebHost.UseUrls("http://localhost:5002"); // local dev için
}

builder.Services.AddControllers();
builder.Services.ConfigureConsul(configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<OcrProcessor>();
builder.Services.AddScoped<OpenAiInterpreter>();
builder.Services.AddHttpClient(); // OpenAI için
// Google Vision için ortam değişkenini burada ayarla
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
builder.Services.ConfigureConsul(configuration);


// Program.cs (builder oluşturduktan hemen sonra)
var visionPath =
    Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") // CI & Docker
    ?? configuration["GoogleVision:CredentialsPath"]                      // local config
    ?? Environment.GetEnvironmentVariable("GOOGLE_VISION_CREDENTIALS");   // eski isim

if (!string.IsNullOrWhiteSpace(visionPath))
{
    // Tek merkez: Vision lib bu env'i okur
    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", visionPath);
}
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCRService v1"));
}

//app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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

app.RegisterWithConsul(app.Lifetime, configuration);

app.Run();
