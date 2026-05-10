using Microsoft.Extensions.Options;
using Sovos.InvoiceWorker.Configuration;
using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Interfaces;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Sovos.InvoiceWorker.Services;

public class BrevoMailSender : IMailSender
{
    private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

    private readonly BrevoOptions _brevo;
    private readonly HttpClient _http;
    private readonly ILogger<BrevoMailSender> _logger;
    private static readonly CultureInfo TrCulture = new("tr-TR");

    public BrevoMailSender(
        IOptions<BrevoOptions> brevoOptions,
        HttpClient http,
        ILogger<BrevoMailSender> logger)
    {
        _brevo = brevoOptions.Value;
        _http = http;
        _logger = logger;
    }

    public async Task SendNewInvoicesAsync(Company company, List<Invoice> newInvoices, CancellationToken ct)
    {
        var recipients = SplitEmails(company.NotificationEmails);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Alıcı yok, mail atlandı: Company={Name} (Id={Id})", company.Name, company.Id);
            return;
        }

        var subject = $"[Sovos] {company.Name} - {newInvoices.Count} yeni onay bekleyen fatura";
        var html = BuildHtmlBody(company, newInvoices);

        await SendAsync(recipients, subject, html, ct);

        _logger.LogInformation("Mail gönderildi: {Emails}, {Count} fatura",
            string.Join(", ", recipients), newInvoices.Count);
    }

    public async Task SendDailySummaryAsync(
        Company company, List<ScrapedInvoice> allPending, CancellationToken ct)
    {
        var subject = allPending.Count > 0
            ? $"[Sovos Günlük Özet] {company.Name} - {allPending.Count} fatura onayda bekliyor"
            : $"[Sovos Günlük Özet] {company.Name} - Onayda bekleyen fatura yok";

        var recipients = SplitEmails(company.NotificationEmails);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Alıcı yok, günlük özet atlandı: Company={Name} (Id={Id})",
                company.Name, company.Id);
            return;
        }

        var html = BuildSummaryHtmlBody(company, allPending);

        await SendAsync(recipients, subject, html, ct);

        var joined = string.Join(", ", recipients);
        if (allPending.Count == 0)
            _logger.LogInformation("Boş özet maili gönderildi: {Emails}", joined);
        else
            _logger.LogInformation(
                "Günlük özet maili gönderildi: {Emails}, {Count} fatura",
                joined, allPending.Count);
    }

    public async Task SendNoNewInvoicesAsync(
        Company company, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var recipients = SplitEmails(company.NotificationEmails);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Alıcı yok, 'fatura yok' maili atlandı: Company={Name} (Id={Id})",
                company.Name, company.Id);
            return;
        }

        var subject = $"[Sovos] {company.Name} - Onayda bekleyen fatura yok";
        var html = MailBodyBuilder.BuildNoNewInvoices(company, fromDate, toDate);

        await SendAsync(recipients, subject, html, ct);

        _logger.LogInformation("'Fatura yok' maili gönderildi: {Emails}", string.Join(", ", recipients));
    }

    public async Task SendLoginErrorAsync(
        Company company, string errorMessage, CancellationToken ct = default)
    {
        var recipients = SplitEmails(company.NotificationEmails);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Alıcı yok, login-hata maili atlandı: Company={Name} (Id={Id})",
                company.Name, company.Id);
            return;
        }

        var subject = $"[Sovos] ❌ {company.Name} - Giriş başarısız";
        var html = MailBodyBuilder.BuildLoginError(company, errorMessage);

        await SendAsync(recipients, subject, html, ct);

        _logger.LogInformation("Login-hata maili gönderildi: {Emails}, Hata={Err}",
            string.Join(", ", recipients), errorMessage);
    }

    public async Task SendGeneralErrorAsync(
        Company company, string errorMessage, CancellationToken ct = default)
    {
        var recipients = SplitEmails(company.NotificationEmails);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Alıcı yok, genel-hata maili atlandı: Company={Name} (Id={Id})",
                company.Name, company.Id);
            return;
        }

        var subject = $"[Sovos] ❌ {company.Name} - Tarama başarısız";
        var html = MailBodyBuilder.BuildGeneralError(company, errorMessage);

        await SendAsync(recipients, subject, html, ct);

        _logger.LogInformation("Genel-hata maili gönderildi: {Emails}, Hata={Err}",
            string.Join(", ", recipients), errorMessage);
    }

    private async Task SendAsync(List<string> recipients, string subject, string htmlContent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_brevo.ApiKey))
            throw new InvalidOperationException("Brevo:ApiKey yapılandırılmamış.");
        if (string.IsNullOrWhiteSpace(_brevo.FromAddress))
            throw new InvalidOperationException("Brevo:FromAddress yapılandırılmamış.");

        var payload = new
        {
            sender = new { name = _brevo.FromDisplayName, email = _brevo.FromAddress },
            to = recipients.Select(e => new { email = e }).ToArray(),
            subject,
            htmlContent
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("api-key", _brevo.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Brevo API çağrısı başarısız (network/transport)");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Brevo API hata yanıtı: Status={Status}, Body={Body}",
                (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"Brevo API {(int)response.StatusCode}: {body}");
        }
    }

    private static List<string> SplitEmails(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .ToList();

    private static string BuildSummaryHtmlBody(Company company, List<ScrapedInvoice> invoices)
    {
        var sb = new StringBuilder();

        sb.Append($"""
            <html><body style="font-family:Arial,sans-serif;font-size:13px">
            <p>{company.Name} firması için günlük onay bekleyen fatura özeti:</p>
            """);

        if (invoices.Count == 0)
        {
            sb.Append("""
                <p style="font-size:14px"><strong>✓ Şu an onayda bekleyen fatura bulunmuyor.</strong></p>
                <p style="color:gray;font-size:11px">Bu, günlük otomatik özet mailidir.</p>
                </body></html>
                """);
            return sb.ToString();
        }

        sb.Append("""
            <table border="1" cellpadding="5" cellspacing="0" style="border-collapse:collapse">
              <thead style="background:#f0f0f0">
                <tr>
                  <th>Fatura No</th>
                  <th>Tedarikçi</th>
                  <th>Tutar</th>
                  <th>Para Birimi</th>
                  <th>Düzenlenme Tarihi</th>
                  <th>Son Ödeme Tarihi</th>
                </tr>
              </thead>
              <tbody>
            """);

        foreach (var inv in invoices)
        {
            sb.Append($"""
                  <tr>
                    <td>{inv.FaturaNo}</td>
                    <td>{inv.FirmaUnvani}</td>
                    <td>{inv.FaturaTutari.ToString("N2", TrCulture)}</td>
                    <td>{inv.ParaBirimi}</td>
                    <td>{inv.DuzenlenmeTarihi?.ToString("dd.MM.yyyy", TrCulture) ?? "-"}</td>
                    <td>{inv.SonOdemeTarihi?.ToString("dd.MM.yyyy", TrCulture) ?? "-"}</td>
                  </tr>
                """);
        }

        sb.Append("</tbody></table>");

        var totals = invoices
            .GroupBy(x => x.ParaBirimi)
            .Select(g => $"{g.Key}: {g.Sum(x => x.FaturaTutari).ToString("N2", TrCulture)}")
            .ToList();

        if (totals.Any())
        {
            sb.Append("<p><strong>Toplam tutarlar:</strong><br/>");
            sb.Append(string.Join("<br/>", totals));
            sb.Append("</p>");
        }

        sb.Append("<p style=\"color:gray;font-size:11px\">Bu, günlük otomatik özet mailidir.</p>");
        sb.Append("</body></html>");

        return sb.ToString();
    }

    private static string BuildHtmlBody(Company company, List<Invoice> invoices)
    {
        var sb = new StringBuilder();

        sb.Append($"""
            <html><body style="font-family:Arial,sans-serif;font-size:13px">
            <p>{company.Name} firması için <strong>{invoices.Count}</strong> yeni onay bekleyen fatura tespit edildi.</p>
            <table border="1" cellpadding="5" cellspacing="0" style="border-collapse:collapse">
              <thead style="background:#f0f0f0">
                <tr>
                  <th>Fatura No</th>
                  <th>Tedarikçi</th>
                  <th>Tutar</th>
                  <th>Para Birimi</th>
                  <th>Düzenlenme Tarihi</th>
                  <th>Son Ödeme Tarihi</th>
                </tr>
              </thead>
              <tbody>
            """);

        foreach (var inv in invoices)
        {
            sb.Append($"""
                  <tr>
                    <td>{inv.FaturaNo}</td>
                    <td>{inv.FirmaUnvani}</td>
                    <td>{inv.FaturaTutari.ToString("N2", TrCulture)}</td>
                    <td>{inv.ParaBirimi}</td>
                    <td>{inv.DuzenlenmeTarihi?.ToString("dd.MM.yyyy", TrCulture) ?? "-"}</td>
                    <td>{inv.SonOdemeTarihi?.ToString("dd.MM.yyyy", TrCulture) ?? "-"}</td>
                  </tr>
                """);
        }

        sb.Append("</tbody></table>");

        var totals = invoices
            .GroupBy(x => x.ParaBirimi)
            .Select(g => $"{g.Key}: {g.Sum(x => x.FaturaTutari).ToString("N2", TrCulture)}")
            .ToList();

        if (totals.Any())
        {
            sb.Append("<p><strong>Toplam tutarlar:</strong><br/>");
            sb.Append(string.Join("<br/>", totals));
            sb.Append("</p>");
        }

        sb.Append("<p style=\"color:gray;font-size:11px\">Bu e-posta otomatik oluşturulmuştur.</p>");
        sb.Append("</body></html>");

        return sb.ToString();
    }
}
