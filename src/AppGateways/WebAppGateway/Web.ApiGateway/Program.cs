using Consul;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Web.ApiGateway.Extensions;
using Web.ApiGateway.Infrastructure;

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

// Ana appsettings config
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/appsettings.json", optional: false)
    .AddJsonFile($"Configurations/appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Environment'e özel ocelot config
var ocelotConfiguration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile($"Configurations/ocelot.{env}.json", optional: false, reloadOnChange: true)
    .Build();

var builder = WebApplication.CreateBuilder(args);

// Appsettings yüklemesi
builder.Configuration.AddConfiguration(configuration);

// Ocelot'u ayrı config ile başlat
builder.Services.AddOcelot(ocelotConfiguration).AddConsul();

// Diğer servis kayıtları
builder.Services.AddControllers();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Web.ApiGateway", Version = "v1" });
});

builder.Services.ConfigureAuth(builder.Configuration);

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient<HttpClientDelegatingHandler>();

builder.Services.AddHttpClient("basket", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["urls:basket"]);
}).AddHttpMessageHandler<HttpClientDelegatingHandler>();

builder.Services.AddHttpClient("catalog", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["urls:catalog"]);
}).AddHttpMessageHandler<HttpClientDelegatingHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web.ApiGateway v1"));
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new()
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(config => config.UIPath = "/hc-ui");

// ✅ Ocelot'u environment'e özel config ile başlatıyoruz
await app.UseOcelot();

app.Run();
