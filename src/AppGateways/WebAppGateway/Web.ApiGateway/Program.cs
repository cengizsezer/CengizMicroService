using Consul;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Web.ApiGateway.Extensions;
using Web.ApiGateway.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Yapılandırma dosyaları
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("Configurations/ocelot.json")
    .AddEnvironmentVariables();

// Servis kayıtları
builder.Services.AddControllers();

// HealthCheck ayarları
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

// Swagger dokümantasyonu
builder.Services.AddSwaggerGen(c => 
{
    c.SwaggerDoc("v1", new() { Title = "Web.ApiGateway", Version = "v1" });
});

// Kimlik doğrulama ayarları
builder.Services.ConfigureAuth(builder.Configuration);

// Ocelot ve Consul entegrasyonu
builder.Services.AddOcelot().AddConsul();

// HTTP context erişimi
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient<HttpClientDelegatingHandler>();

// CORS politikası (Geliştirme ortamı için geniş ayarlar)
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

// Geliştirme ortamı özellikleri
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web.ApiGateway v1"));
}

// Middleware pipeline
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("CorsPolicy");

// Kimlik doğrulama ve yetkilendirme
app.UseAuthentication();
app.UseAuthorization();

// Endpoint yapılandırması
app.MapControllers();

// HealthCheck endpoint'leri
app.MapHealthChecks("/health", new()
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(config => config.UIPath = "/hc-ui");

// Ocelot middleware'i
await app.UseOcelot();

app.Run();