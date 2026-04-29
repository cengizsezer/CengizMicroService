using System.Diagnostics;
using Sovos.InvoiceWorker.Core.Interfaces;

namespace Sovos.InvoiceWorker.Workers;

public class InvoiceCheckWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<InvoiceCheckWorker> _logger;

    private DateTime _lastDailyRun = DateTime.MinValue;

    public InvoiceCheckWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<InvoiceCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var initialDelay = _config.GetValue<int>("Worker:InitialDelaySeconds", 30);
        var dailySummaryHour = _config.GetValue<int>("Worker:DailySummaryHour", 9);

        _logger.LogInformation(
            "InvoiceCheckWorker başladı. InitialDelay={Delay}sn, DailyHour={Hour}",
            initialDelay, dailySummaryHour);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(initialDelay), ct);
        }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.Now; // TR yerel saati — schedule + DailySummary buna göre

            // Schedule modlarına göre per-firma tarama (Hourly / Daily / Manual)
            await RunSafelyAsync("ScheduledChecks",
                o => o.RunScheduledChecksAsync(ct), ct);

            // Günlük özet (schedule modlarından bağımsız — sabah belirli saatte 1 kez)
            if (now.Hour == dailySummaryHour && _lastDailyRun.Date != now.Date)
            {
                await RunSafelyAsync("DailySummary",
                    o => o.RunDailySummaryAsync(ct), ct);
                _lastDailyRun = now;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunSafelyAsync(
        string jobName, Func<IInvoiceOrchestrator, Task> action, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider
                .GetRequiredService<IInvoiceOrchestrator>();

            _logger.LogInformation("=== {JobName} BAŞLADI ===", jobName);
            var sw = Stopwatch.StartNew();
            await action(orchestrator);
            sw.Stop();
            _logger.LogInformation(
                "=== {JobName} BİTTİ ({Duration}ms) ===", jobName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{JobName} hata fırlattı, döngü devam edecek", jobName);
        }
    }
}
