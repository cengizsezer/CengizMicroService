using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Factory;
using IdentityService.IntegrationEvents.Event;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.Console.Email;
using NotificationService.Console.IntegrationEvents.EventHandlers;
using NotificationService.Console.Persistence;
using NotificationService.Console.Worker;
using Quartz;
using RabbitMQ.Client;
using Serilog;

namespace NotificationService;

public class Program
{
    private static readonly string Env =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

    public static async Task Main(string[] args)
    {
#if DEBUG
        // Serilog kendi hatalarını konsola yazsın (DNS/Graylog vs.)
        Serilog.Debugging.SelfLog.Enable(m =>
            System.Console.Error.WriteLine("SERILOG-SELFLOG: " + m));
#endif

        var appConfig = BuildConfiguration();
        var serilogConfig = BuildSerilogConfiguration();

        // Global logger (Host başlamadan önce)
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(serilogConfig)
            .CreateLogger();
        Log.Information("NS fallback console logger OK");
        try
        {
            Log.Information("NotificationService starting... (env: {Env})", Env);

            var host = Host.CreateDefaultBuilder(args)
                // Serilog’u Host logging pipeline’ına bağla (CONFIG’TEN)
                .UseSerilog((ctx, services, loggerCfg) =>
                    loggerCfg.ReadFrom.Configuration(serilogConfig)
                             .Enrich.FromLogContext())
                .ConfigureServices((ctx, services) =>
                {
                    // IConfiguration
                    services.AddSingleton<IConfiguration>(appConfig);

                    // DbContext + Factory
                    services.AddDbContext<NotificationDbContext>(opt =>
                        opt.UseSqlServer(appConfig.GetConnectionString("DatabaseConnection")));
                    services.AddDbContextFactory<NotificationDbContext>(opt =>
                        opt.UseSqlServer(appConfig.GetConnectionString("DatabaseConnection")));

                    // Email
                    services.Configure<SmtpOptions>(appConfig.GetSection("Smtp"));
                    services.AddSingleton<IEmailSender, SmtpEmailSender>();

                    // Event handlers
                    services.AddTransient<UserRegisterSuccessIntegrationEventHandler>();
                    services.AddTransient<UserRegisterFailedIntegrationEventHandler>();
                    services.AddTransient<TaskCreatedHandler>();
                    services.AddTransient<TaskUpdatedHandler>();
                    services.AddTransient<TaskDeletedHandler>();

                    // Quartz: her gün 08:00 (Europe/Istanbul)
                    services.AddQuartz(q =>
                    {
                        q.UseMicrosoftDependencyInjectionJobFactory();

                        var jobKey = new JobKey("DailyRemindersJob");
                        q.AddJob<DailyRemindersJob>(o => o.WithIdentity(jobKey));
                        q.AddTrigger(t => t
          .ForJob(jobKey)
          .WithIdentity("DailyReminders_DevEveryMinute")
          .StartNow()
          .WithSimpleSchedule(x => x
              .WithIntervalInMinutes(1)
              .RepeatForever()
              .WithMisfireHandlingInstructionFireNow())
      );
                        //q.AddTrigger(t => t
                        //    .ForJob(jobKey)
                        //    .WithIdentity("DailyRemindersTrigger")
                        //    .WithCronSchedule("0 0 8 * * ?", x => x
                        //        .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"))
                        //        .WithMisfireHandlingInstructionFireAndProceed()));
                    });
                    services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);

                    // EventBus (RabbitMQ)
                    services.AddSingleton<IEventBus>(sp =>
                    {
                        var hostName = appConfig["RabbitMQ:HostName"] ?? "localhost";
                        var port = int.TryParse(appConfig["RabbitMQ:Port"], out var p) ? p : 5672;
                        var user = appConfig["RabbitMQ:UserName"] ?? "guest";
                        var pass = appConfig["RabbitMQ:Password"] ?? "guest";

                        var factory = new ConnectionFactory
                        {
                            HostName = hostName,
                            Port = port,
                            UserName = user,
                            Password = pass,
                            AutomaticRecoveryEnabled = true
                        };

                        var eb = new EventBusConfig
                        {
                            ConnectionRetryCount = 5,
                            EventNameSuffix = "IntegrationEvent",
                            SubscriberClientAppName = "NotificationService",
                            EventBusType = EventBusType.RabbitMQ,
                            Connection = factory
                        };

                        return EventBusFactory.Create(eb, sp);
                    });

                    // EventBus subscribe’ları host start’ında aç
                    services.AddHostedService<EventBusSubscriptionHostedService>();
                })
                .Build();

            Log.Information("NotificationService started (env: {Env})", Env);

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "NotificationService terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Configurations/appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"Configurations/appsettings.{Env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

    private static IConfiguration BuildSerilogConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Configurations/serilog.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"Configurations/serilog.{Env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
}
