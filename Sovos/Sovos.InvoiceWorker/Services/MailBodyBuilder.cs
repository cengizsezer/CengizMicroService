using System.Globalization;
using System.Net;
using System.Text;
using Sovos.InvoiceWorker.Core.Entities;

namespace Sovos.InvoiceWorker.Services;

internal static class MailBodyBuilder
{
    private static readonly CultureInfo TrCulture = new("tr-TR");

    public static string BuildNoNewInvoices(Company company, DateTime fromDate, DateTime toDate)
    {
        var name = WebUtility.HtmlEncode(company.Name);
        var now = DateTime.Now.ToString("dd.MM.yyyy HH:mm", TrCulture);
        var from = fromDate.ToString("dd.MM.yyyy", TrCulture);
        var to = toDate.ToString("dd.MM.yyyy", TrCulture);

        var sb = new StringBuilder();
        sb.Append($"""
            <html><body style="font-family:Arial,sans-serif;font-size:13px">
              <p>{name} firması için tarama tamamlandı.</p>
              <p style="font-size:14px"><strong>✓ Belirtilen aralıkta onayda bekleyen fatura bulunamadı.</strong></p>
              <p><strong>Tarama aralığı:</strong> {from} - {to}</p>
              <p><strong>Tarama zamanı:</strong> {now}</p>
              <p style="color:gray;font-size:11px">Bu e-posta otomatik oluşturulmuştur.</p>
            </body></html>
            """);
        return sb.ToString();
    }

    public static string BuildLoginError(Company company, string errorMessage)
    {
        var name = WebUtility.HtmlEncode(company.Name);
        var err = WebUtility.HtmlEncode(errorMessage ?? string.Empty);
        var now = DateTime.Now.ToString("dd.MM.yyyy HH:mm", TrCulture);

        var sb = new StringBuilder();
        sb.Append($"""
            <html><body style="font-family:Arial,sans-serif;font-size:13px">
              <div style="border-left:4px solid #d9534f;padding:8px 12px;background:#fdf2f2">
                <p style="margin:0 0 8px 0"><strong style="color:#a94442">DİKKAT:</strong> {name} firması için Sovos portalına giriş yapılamadı.</p>
                <p style="margin:0 0 8px 0"><strong>Hata:</strong> {err}</p>
                <p style="margin:8px 0 4px 0"><strong>Bu durumda yapabilecekleriniz:</strong></p>
                <ul style="margin:0 0 8px 18px;padding:0">
                  <li>Şifrenin ve kullanıcı adının doğru olduğunu kontrol edin</li>
                  <li>Birkaç dakika sonra manuel olarak tekrar deneyin</li>
                  <li>Sovos portal sitesinin erişilebilir olduğunu kontrol edin</li>
                  <li>Sorun devam ederse teknik destek ile iletişime geçin</li>
                </ul>
                <p style="margin:0"><strong>Tarama zamanı:</strong> {now}</p>
              </div>
              <p style="color:gray;font-size:11px">Bu e-posta otomatik oluşturulmuştur.</p>
            </body></html>
            """);
        return sb.ToString();
    }

    public static string BuildGeneralError(Company company, string errorMessage)
    {
        var name = WebUtility.HtmlEncode(company.Name);
        var err = WebUtility.HtmlEncode(errorMessage ?? string.Empty);
        var now = DateTime.Now.ToString("dd.MM.yyyy HH:mm", TrCulture);

        var sb = new StringBuilder();
        sb.Append($"""
            <html><body style="font-family:Arial,sans-serif;font-size:13px">
              <div style="border-left:4px solid #d9534f;padding:8px 12px;background:#fdf2f2">
                <p style="margin:0 0 8px 0"><strong style="color:#a94442">DİKKAT:</strong> {name} firması için tarama tamamlanamadı.</p>
                <p style="margin:0 0 8px 0"><strong>Hata:</strong> {err}</p>
                <p style="margin:8px 0 4px 0"><strong>Bu durumda yapabilecekleriniz:</strong></p>
                <ul style="margin:0 0 8px 18px;padding:0">
                  <li>Birkaç dakika sonra manuel olarak tekrar deneyin</li>
                  <li>Sovos portal sitesinin erişilebilir olduğunu kontrol edin</li>
                  <li>Sorun devam ederse teknik destek ile iletişime geçin</li>
                </ul>
                <p style="margin:0"><strong>Tarama zamanı:</strong> {now}</p>
              </div>
              <p style="color:gray;font-size:11px">Bu e-posta otomatik oluşturulmuştur.</p>
            </body></html>
            """);
        return sb.ToString();
    }
}
