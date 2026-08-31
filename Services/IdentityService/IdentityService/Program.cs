using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Factory;
using HealthChecks.UI.Client;
using IdentityService.Application.ConsulRegistration;
using IdentityService.Application.DbRegistration;
using IdentityService.Application.Services;
using IdentityService.Application.Services.Agent;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/appsettings.json", optional: false)
    .AddJsonFile($"Configurations/appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var serilogConfig = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("Configurations/serilog.json", optional: false)
    .AddJsonFile($"Configurations/serilog.{env}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(serilogConfig)
    .CreateLogger();

try
{
    Log.Information("Starting web host...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddConfiguration(configuration);
    builder.Host.UseSerilog();

    // DbContext
    builder.Services.AddDbContext<IdentityDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection")));
    //builder.Services.ConfigureDbContext(configuration);
    // Services
    builder.Services.AddIdentityCore<User>(o =>
    {
        o.User.RequireUniqueEmail = true;
    })
.AddRoles<IdentityRole<int>>()
.AddEntityFrameworkStores<IdentityDbContext>()
.AddSignInManager<SignInManager<User>>();

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
              ValidateIssuerSigningKey = true,
              ValidateLifetime = true,
              ValidIssuer = builder.Configuration["Jwt:Issuer"],
              ValidAudience = builder.Configuration["Jwt:Audience"],
              IssuerSigningKey = new SymmetricSecurityKey(
                  Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!)
              ),
              RoleClaimType = ClaimTypes.Role // [Authorize(Roles="…")] için
          };
      });
    //// Authentication
    //builder.Services
    // .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    // .AddJwtBearer(o =>
    // {
    //     o.RequireHttpsMetadata = false; // DEV için, prod'da true yap
    //     o.TokenValidationParameters = new TokenValidationParameters
    //     {
    //         ValidateIssuer = true,
    //         ValidateAudience = true,
    //         ValidateLifetime = true,
    //         ValidateIssuerSigningKey = true,

    //         ValidIssuer = builder.Configuration["Jwt:Issuer"],        // identityserver.tr
    //         ValidAudience = builder.Configuration["Jwt:Audience"],    // identityclient.tr
    //         IssuerSigningKey = new SymmetricSecurityKey(
    //             Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]!) // <--- anahtar adı düzeltildi
    //         )
    //     };
    // });



    builder.Services.AddSingleton<IEventBus>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var host = cfg["RabbitMQ:HostName"] ?? "localhost";
        var port = int.TryParse(cfg["RabbitMQ:Port"], out var p) ? p : 5672;

        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = cfg["RabbitMQ:UserName"] ?? "guest",
            Password = cfg["RabbitMQ:Password"] ?? "guest",
            AutomaticRecoveryEnabled = true
        };

        var ebCfg = new EventBusConfig
        {
            ConnectionRetryCount = 5,
            EventNameSuffix = "IntegrationEvent",
            SubscriberClientAppName = builder.Environment.ApplicationName,
            EventBusType = EventBusType.RabbitMQ,
            Connection = factory
        };

        return EventBusFactory.Create(ebCfg, sp);
    });


    builder.Services.AddAuthorization(AjanYetkileri.Ekle);

    builder.Services.AddScoped<IIdentityService, IdentityService.Application.Services.IdentityService>();

    // Ajan kimliği: uzun ömürlü anahtar -> 8 saatlik ajan token'ı.
    // Anahtar hash'i parolayla aynı hasher'dan geçiyor (PBKDF2); ayrı bir
    // algoritma seçmemek için Identity'nin kendi hasher'ı kaydediliyor.
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<IPasswordHasher<Ajan>, PasswordHasher<Ajan>>();
    builder.Services.AddScoped<IAjanKimlikServisi, AjanKimlikServisi>();

    // Ajan token ucu anonim; anahtar denemesine karşı hız sınırı var.
    builder.Services.AddRateLimiter(AjanHizSiniri.Ekle);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "IdentityService", Version = "v1" });

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
        {
            securityScheme,
            new[] { "Bearer" }
        }
    });
    });

    if (env == "Docker")
    {
        builder.WebHost.UseUrls("http://0.0.0.0:5005"); // container dışına açıl
    }
    else
    {
        builder.WebHost.UseUrls("http://localhost:5005"); // local dev için
    }
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy());

    builder.Services.ConfigureConsul(configuration);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    var host = app as IHost;   // Veya doğrudan builder.Build()'ten sonra kullan

    host.MigrateDbContext<IdentityDbContext>((context, services) =>
    {
        var logger = services.GetRequiredService<ILogger<IdentityContextSeed>>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var seeder = new IdentityContextSeed();
        seeder.SeedAsync(context, env, logger,true).Wait();
    });

    // ... MapControllers, MapHealthChecks, RegisterWithConsul vs. SONRASINA, app.Run()'DAN ÖNCE
    using (var scope = app.Services.CreateScope())
    {
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<Program>>();
        var userMgr = sp.GetRequiredService<UserManager<User>>();
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole<int>>>();

        // 1) Admin rolü garanti
        if (!await roleMgr.RoleExistsAsync("Admin"))
        {
            var createRole = await roleMgr.CreateAsync(new IdentityRole<int>("Admin"));
            if (!createRole.Succeeded)
                logger.LogError("Admin rolü oluşturulamadı: {Err}",
                    string.Join(", ", createRole.Errors.Select(e => e.Description)));
        }

        // 2) admin kullanıcısını bul
        var admin = await userMgr.FindByNameAsync("admin"); // veya FindByEmailAsync("admin@example.com")
        if (admin is null)
        {
            logger.LogWarning("admin kullanıcısı bulunamadı, şimdi oluşturalım.");
            var create = await userMgr.CreateAsync(new User
            {
                UserName = "admin",
                Email = "admin@example.com",
                EmailConfirmed = true
            }, "Admin123!");
            if (!create.Succeeded)
            {
                logger.LogError("admin oluşturulamadı: {Err}",
                    string.Join(", ", create.Errors.Select(e => e.Description)));
            }
            else
            {
                admin = await userMgr.FindByNameAsync("admin");
            }
        }

        // 3) Role ekle
        if (admin != null && !await userMgr.IsInRoleAsync(admin, "Admin"))
        {
            var addRole = await userMgr.AddToRoleAsync(admin, "Admin");
            if (!addRole.Succeeded)
                logger.LogError("admin -> Admin rolü atanamadı: {Err}",
                    string.Join(", ", addRole.Errors.Select(e => e.Description)));
            else
                logger.LogInformation("admin kullanıcısına Admin rolü atandı.");
        }
    }



    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IdentityService v1"));
    }
   
    //app.UseHttpsRedirection();
    app.UseRouting();
    app.UseRateLimiter();
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

  
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
