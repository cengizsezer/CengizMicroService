using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Sovos.InvoiceWorker.Core.Interfaces;
using SovosService.Api.Application.ConsulRegistration;
using SovosService.Api.Persistence;
using SovosService.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// DbContext
builder.Services.AddDbContext<SovosServiceDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection")));

// DataProtection — keys must be shared with Sovos.InvoiceWorker so credentials
// encrypted by either side can be decrypted by the other.
var dpKeysFolder = builder.Configuration["DataProtection:KeysFolder"]
    ?? throw new InvalidOperationException("DataProtection:KeysFolder yapılandırması eksik.");
var dpAppName = builder.Configuration["DataProtection:ApplicationName"]
    ?? "SovosInvoiceWorker";
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysFolder))
    .SetApplicationName(dpAppName);

builder.Services.AddScoped<ICredentialProtector, CredentialProtector>();

// Worker'a (Sovos.InvoiceWorker) HTTP çağrıları için named client.
// BaseUrl: lokalde http://localhost:5011, Docker'da http://c_sovos_invoiceworker:5011.
var workerBaseUrl = builder.Configuration["Worker:BaseUrl"]
    ?? throw new InvalidOperationException("Worker:BaseUrl yapılandırması eksik.");
builder.Services.AddHttpClient("Worker", c =>
{
    c.BaseAddress = new Uri(workerBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(30);
});

// JWT — IdentityService ile aynı SigningKey/Issuer/Audience kullanılıyor.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)
            ),
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// Health checks (Consul /liveness için "self")
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

// Consul registration
builder.Services.ConfigureConsul(builder.Configuration);

// Controllers + Swagger (Bearer auth)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SovosService.Api", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new[] { "Bearer" } }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Port binding — Docker dışına açıl, local'de localhost
var env = builder.Environment.EnvironmentName;
if (env == "Docker")
{
    builder.WebHost.UseUrls("http://0.0.0.0:5010");
}
else
{
    builder.WebHost.UseUrls("http://localhost:5010");
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SovosService.Api v1"));

app.UseRouting();
app.UseCors("AllowAll");
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

app.RegisterWithConsul(app.Lifetime, builder.Configuration);

app.Run();
