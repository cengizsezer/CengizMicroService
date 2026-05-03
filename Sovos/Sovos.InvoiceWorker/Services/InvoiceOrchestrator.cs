using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public async Task RunForCompanyAsync(int companyId, bool manualMode, CancellationToken ct)
    {
        var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new ArgumentException($"Firma bulunamadı: Id={companyId}");

        await ProcessHourlyAsync(company, ct, manualMode);
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

        for (int i = 0; i < due.Count; i++)
        {
            if (i > 0)
                await Task.Delay(InterCompanyDelayMs, ct);

            var company = due[i];
            if (company.ScheduleMode == ScheduleMode.Daily)
                await ProcessDailyAsync(company, ct);
            else
                await ProcessHourlyAsync(company, ct);
        }
    }

    private static bool IsDue(Company c, DateTime nowLocal)
    {
        var lastUtc = MaxNullable(c.LastSuccessfulRunAt, c.LastFailedRunAt);
        var lastLocal = lastUtc.HasValue
            ? DateTime.SpecifyKind(lastUtc.Value, DateTimeKind.Utc).ToLocalTime()
            : DateTime.MinValue;

        return c.ScheduleMode switch
        {
            ScheduleMode.Hourly =>
                (nowLocal - lastLocal) >= TimeSpan.FromHours(1),
            ScheduleMode.Daily =>
                nowLocal.Hour == (c.ScheduleHour ?? 9)
                && lastLocal.Date < nowLocal.Date,
            ScheduleMode.Manual =>
                false,
            _ => false
        };
    }

    private static DateTime? MaxNullable(DateTime? a, DateTime? b) => (a, b) switch
    {
        (null, null) => null,
        (var x, null) => x,
        (null, var y) => y,
        (var x, var y) => x > y ? x : y
    };

    // ── Per-company iş akışları ──────────────────────────────────────────

    private async Task ProcessHourlyAsync(Company company, CancellationToken ct, bool manualMode = false)
    {
        try
        {
            var password = _protector.Decrypt(company.EncryptedPassword);
            var scraped = await _scraper.FetchPendingInvoicesAsync(company, password, ct);
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

    private async Task ProcessDailyAsync(Company company, CancellationToken ct)
    {
        try
        {
            var password = _protector.Decrypt(company.EncryptedPassword);
            var scraped = await _scraper.FetchPendingInvoicesAsync(company, password, ct);

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
