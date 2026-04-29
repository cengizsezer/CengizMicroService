using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Sovos.InvoiceWorker.Configuration;
using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Interfaces;
using System.Globalization;
using System.Text;

namespace Sovos.InvoiceWorker.Services;

public class MailSender : IMailSender
{
    private readonly SmtpOptions _smtp;
    private readonly ILogger<MailSender> _logger;
    private static readonly CultureInfo TrCulture = new("tr-TR");

    public MailSender(IOptions<SmtpOptions> smtpOptions, ILogger<MailSender> logger)
    {
        _smtp = smtpOptions.Value;
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

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromDisplayName, _smtp.FromAddress));
        foreach (var email in recipients)
            message.To.Add(MailboxAddress.Parse(email));
        message.Subject = $"[Sovos] {company.Name} - {newInvoices.Count} yeni onay bekleyen fatura";
        message.Body = new TextPart("html") { Text = BuildHtmlBody(company, newInvoices) };

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port,
            _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
        await client.AuthenticateAsync(_smtp.Username, _smtp.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

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

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromDisplayName, _smtp.FromAddress));
        foreach (var email in recipients)
            message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = BuildSummaryHtmlBody(company, allPending) };

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port,
            _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
        await client.AuthenticateAsync(_smtp.Username, _smtp.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        var joined = string.Join(", ", recipients);
        if (allPending.Count == 0)
            _logger.LogInformation("Boş özet maili gönderildi: {Emails}", joined);
        else
            _logger.LogInformation(
                "Günlük özet maili gönderildi: {Emails}, {Count} fatura",
                joined, allPending.Count);
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

        // Para birimine göre toplam
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
