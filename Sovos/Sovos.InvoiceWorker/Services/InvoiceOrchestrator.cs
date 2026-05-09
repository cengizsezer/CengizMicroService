using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Enums;
using Sovos.InvoiceWorker.Core.Interfaces;
using Sovos.InvoiceWorker.Data;

namespace Sovos.InvoiceWorker.Services;

public class InvoiceOrchestrator : IInvoiceOrchestrator
{
    private readonly SovosDbContext _db;
    private readonly ISovosScraper _scraper;
    private readonly IInvoiceDiffService _diff;
    private readonly IMailSender _mail;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<InvoiceOrchestrator> _logger;

    private const int InterCompanyDelayMs = 5000;

    public InvoiceOrchestrator(
        SovosDbContext db,
        ISovosScraper scraper,
        IInvoiceDiffService diff,
        IMailSender mail,
        ICredentialProtector protector,
        ILogger<InvoiceOrchestrator> logger)
    {
        _db = db;
        _scraper = scraper;
        _diff = diff;
        _mail = mail;
        _protector = protector;
        _logger = logger;
    }

    public async Task RunForCompanyAsync(
        int companyId,
        bool manualMode,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new ArgumentException($"Firma bulunamadı: Id={companyId}");

        await ProcessHourlyAsync(company, fromDate, toDate, ct, manualMode);
    }

    private static (DateTime From, DateTime To) DefaultRange()
    {
        var today = DateTime.Today;
        return (today.AddMonths(-1), today);
    }

    public async Task<BatchRunResult> RunBatchAsync(
        IReadOnlyList<int> companyIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct)
    {
        var result = new BatchRunResult();

        var companies = await _db.Companies
            .Where(c => companyIds.Contains(c.Id))
            .ToListAsync(ct);

        var byId = companies.ToDictionary(c => c.Id);

        _logger.LogInformation(
            "RunBatch: {Requested} firma istendi, {Found} bulundu, aralık {From:dd.MM.yyyy} - {To:dd.MM.yyyy}",
            companyIds.Count, companies.Count, fromDate, toDate);

        for (int i = 0; i < companyIds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (i > 0)
                await Task.Delay(InterCompanyDelayMs, ct);

            var id = companyIds[i];
            if (!byId.TryGetValue(id, out var company))
            {
                result.Failed.Add(new BatchRunItem
                {
                    CompanyId = id,
                    CompanyName = $"#{id}",
                    Error = "Firma bulunamadı"
                });
                continue;
            }

            try
            {
                await ProcessHourlyAsync(company, fromDate, toDate, ct, manualMode: true);

                if (company.LastErrorMessage is { Length: > 0 } err)
                {
                    result.Failed.Add(new BatchRunItem
                    {
                        CompanyId = company.Id,
                        CompanyName = company.Name,
                        Error = err
                    });
                }
                else
                {
                    result.Success.Add(new BatchRunItem
                    {
                        CompanyId = company.Id,
                        CompanyName = company.Name
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "RunBatch: Firma {Name} (Id={Id}) işlenirken hata", company.Name, company.Id);
                result.Failed.Add(new BatchRunItem
                {
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    Error = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "RunBatch tamamlandı: {Ok} başarılı, {Fail} başarısız",
            result.Success.Count, result.Failed.Count);

        return result;
    }

    // Her dakika InvoiceCheckWorker tarafından çağrılır.
    // Her firmanın ScheduleMode + ScheduleHour'ına bakarak gerekli olanları tarar.
    public async Task RunScheduledChecksAsync(CancellationToken ct)
    {
        var nowLocal = DateTime.Now;
        var companies = await _db.Companies
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        var due = new List<Company>();
        foreach (var c in companies)
        {
            if (IsDue(c, nowLocal)) due.Add(c);
        }

        if (due.Count == 0) return;

        _logger.LogInformation(
            "ScheduledChecks: {Total} aktif firma, {Due} tanesi şu an taranacak",
            companies.Count, due.Count);

        var (defaultFrom, defaultTo) = DefaultRange();

        for (int i = 0; i < due.Count; i++)
        {
            if (i > 0)
                await Task.Delay(InterCompanyDelayMs, ct);

            var company = due[i];
            if (company.ScheduleMode == ScheduleMode.Daily)
                await ProcessDailyAsync(company, defaultFrom, defaultTo, ct);
            else
                await ProcessHourlyAsync(company, defaultFrom, defaultTo, ct);
        }
    }

    private bool IsDue(Company c, DateTime nowLocal)
    {
        var lastUtc = MaxNullable(c.LastSuccessfulRunAt, c.LastFailedRunAt);
        DateTime? lastLocal = lastUtc.HasValue
            ? DateTime.SpecifyKind(lastUtc.Value, DateTimeKind.Utc).ToLocalTime()
            : null;

        var schedHour = c.ScheduleHour ?? 9;

        bool result = c.ScheduleMode switch
        {
            ScheduleMode.Hourly =>
                lastLocal is null
                || (nowLocal - lastLocal.Value) >= TimeSpan.FromHours(1),
            ScheduleMode.Daily =>
                nowLocal.Hour >= schedHour
                && (lastLocal is null || lastLocal.Value.Date < nowLocal.Date),
            ScheduleMode.Manual =>
                false,
            _ => false
        };

        _logger.LogInformation(
            "🧮 IsDue {Mode} | Id={Id} nowHour={NH} schedHour={SH} lastDate={LD} nowDate={ND} → {R}",
            c.ScheduleMode, c.Id, nowLocal.Hour, schedHour,
            lastLocal?.Date.ToString("yyyy-MM-dd") ?? "NULL",
            nowLocal.Date.ToString("yyyy-MM-dd"),
            result);

        return result;
    }

    private static DateTime? MaxNullable(DateTime? a, DateTime? b) => (a, b) switch
    {
        (null, null) => null,
        (var x, null) => x,
        (null, var y) => y,
        (var x, var y) => x > y ? x : y
    };

    // ── Per-company iş akışları ──────────────────────────────────────────

    private async Task ProcessHourlyAsync(
        Company company,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct,
        bool manualMode = false)
    {
        try
        {
            var password = _protector.Decrypt(company.EncryptedPassword);
            var scraped = await _scraper.FetchPendingInvoicesAsync(company, password, fromDate, toDate, ct);
            var toNotify = await _diff.SaveAndGetNewAsync(company.Id, scraped, ct, manualMode);

            if (toNotify.Count > 0)
            {
                try
                {
                    await _mail.SendNewInvoicesAsync(company, toNotify, ct);
                    await _diff.MarkAsNotifiedAsync(toNotify.Select(i => i.Id), ct);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx,
                        "Firma {CompanyName}: Yeni fatura mail başarısız (NotifiedAt=null kaldı)",
                        company.Name);
                }
            }

            company.LastSuccessfulRunAt = DateTime.UtcNow;
            company.LastErrorMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firma {CompanyName}: Tarama hata (manualMode={Manual})",
                company.Name, manualMode);
            company.LastFailedRunAt = DateTime.UtcNow;
            company.LastErrorMessage = Truncate(ex.Message, 2000);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ProcessDailyAsync(
        Company company,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct)
    {
        try
        {
            var password = _protector.Decrypt(company.EncryptedPassword);
            var scraped = await _scraper.FetchPendingInvoicesAsync(company, password, fromDate, toDate, ct);

            // Saatlikten kaçanları yakala — yeni/retry mail akışını çalıştır
            var toNotify = await _diff.SaveAndGetNewAsync(company.Id, scraped, ct);
            if (toNotify.Count > 0)
            {
                try
                {
                    await _mail.SendNewInvoicesAsync(company, toNotify, ct);
                    await _diff.MarkAsNotifiedAsync(toNotify.Select(i => i.Id), ct);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx,
                        "Firma {CompanyName}: Daily özet öncesi yeni-fatura maili başarısız",
                        company.Name);
                }
            }

            // Sonra: tüm scraped → günlük özet (boş olsa bile spec gereği gönder)
            await _mail.SendDailySummaryAsync(company, scraped, ct);

            company.LastSuccessfulRunAt = DateTime.UtcNow;
            company.LastErrorMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firma {CompanyName}: DailySummary hata", company.Name);
            company.LastFailedRunAt = DateTime.UtcNow;
            company.LastErrorMessage = Truncate(ex.Message, 2000);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
