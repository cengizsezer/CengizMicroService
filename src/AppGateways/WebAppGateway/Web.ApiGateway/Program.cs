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

// Config dosyalarını yükle
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("Configurations/ocelot.json")
    .AddEnvironmentVariables();

// Servisleri ekle
builder.Services.AddControllers();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());
// .AddUrlGroup(new Uri("http://localhost:5005/hc"), name: "identityapi-check", tags: new[] { "identityapi" });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Web.ApiGateway", Version = "v1" });
});

//builder.Services.AddScoped<ICatalogService, CatalogService>();
//builder.Services.AddScoped<IBasketService, BasketService>();

builder.Services.ConfigureAuth(builder.Configuration);

builder.Services.AddOcelot().AddConsul();

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

// Ortam ayarları
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

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHealthChecksUI();
});

app.UseHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseHealthChecksUI(config => config.UIPath = "/hc-ui");

await app.UseOcelot();

app.Run();
