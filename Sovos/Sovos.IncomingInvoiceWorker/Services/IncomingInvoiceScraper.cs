using System.Globalization;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Sovos.IncomingInvoiceWorker.Configuration;
using Sovos.IncomingInvoiceWorker.Services.Parsing;
using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Exceptions;

namespace Sovos.IncomingInvoiceWorker.Services;

// SovosScraper (onaylı fatura) ile birebir aynı pattern. Farklar:
//   1) Hedef URL: PortalIncomingInvoicesUrl (Inbox.aspx) — sekme/menü tıklama yok.
//   2) Grid satırları DOM'dan parse edilmez; #exportToXLSXButton tıklanarak
//      indirilen XLSX, GelenFaturaExcelParser ile parse edilir.
public class IncomingInvoiceScraper : IIncomingInvoiceScraper
{
    private readonly IncomingSovosOptions _options;
    private readonly IGelenFaturaExcelParser _excelParser;
    private readonly ILogger<IncomingInvoiceScraper> _logger;
    private static readonly CultureInfo TrCulture = new("tr-TR");

    public IncomingInvoiceScraper(
        IOptions<IncomingSovosOptions> options,
        IGelenFaturaExcelParser excelParser,
        ILogger<IncomingInvoiceScraper> logger)
    {
        _options = options.Value;
        _excelParser = excelParser;
        _logger = logger;
    }

    public async Task<List<ScrapedInvoice>> FetchIncomingInvoicesAsync(
        Company company,
        string decryptedPassword,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Firma {CompanyName}: Gelen Fatura taraması başlıyor (aralık {From:dd.MM.yyyy} - {To:dd.MM.yyyy})",
            company.Name, fromDate, toDate);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.SlowMoMs
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(_options.DefaultTimeoutMs);

        await LoginWithRetryAsync(page, company, decryptedPassword, ct);
        await NavigateToIncomingInvoicesAsync(page, company);
        await ApplyDateFilterAsync(page, company, fromDate, toDate);
        var invoices = await DownloadAndParseExcelAsync(page, company);
        await LogoutAsync(page, company);

        sw.Stop();
        _logger.LogInformation(
            "Firma {CompanyName}: Tarama tamamlandı. {Count} gelen fatura. Süre: {Ms}ms",
            company.Name, invoices.Count, sw.ElapsedMilliseconds);

        return invoices;
    }

    // ── Tek fatura PDF indir ─────────────────────────────────────────────────
    // Portalda DENEYEREK doğrulandı (PKF Strateji, 06/2026):
    //   sağ tık → "Yazdır/İndir" ▶ → "Tek Tek PDF İndir" bir ZIP indirir,
    //   ZIP içinde tek bir PDF entry bulunur. Login+nav+filtre aynı yardımcılar.

    public async Task<InvoicePdfResult> DownloadInvoicePdfAsync(
        Company company,
        string decryptedPassword,
        DateTime fromDate,
        DateTime toDate,
        string faturaNo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(faturaNo))
            throw new ArgumentException("Fatura no boş olamaz.", nameof(faturaNo));

        _logger.LogInformation(
            "Firma {CompanyName}: Tek fatura PDF indirme başlıyor (FaturaNo={FaturaNo})",
            company.Name, faturaNo);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.SlowMoMs
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(_options.DefaultTimeoutMs);

        // FetchIncomingInvoicesAsync ile birebir aynı 3 adım:
        await LoginWithRetryAsync(page, company, decryptedPassword, ct);
        await NavigateToIncomingInvoicesAsync(page, company);
        await ApplyDateFilterAsync(page, company, fromDate, toDate);

        // Grid'de fatura no satırını bul (SovosScraper map'i: :scope>td.dxgv[2]=FaturaNo).
        var targetRow = await FindInvoiceRowAsync(page, faturaNo);
        if (targetRow is null)
        {
            await LogoutAsync(page, company);
            throw new InvalidOperationException(
                $"Fatura '{faturaNo}' verilen tarih aralığında ({fromDate:dd.MM.yyyy}-{toDate:dd.MM.yyyy}) " +
                "grid'de bulunamadı. Tarih aralığını fatura tarihini kapsayacak şekilde verin.");
        }

        // Sağ tık → context menü → "Yazdır/İndir" hover → "Tek Tek PDF İndir" (ZIP indirir).
        await targetRow.ScrollIntoViewIfNeededAsync();
        await targetRow.ClickAsync(new ElementHandleClickOptions { Button = MouseButton.Right });

        await page.GetByText("Yazdır/İndir", new PageGetByTextOptions { Exact = true }).First
            .HoverAsync();

        IDownload download;
        try
        {
            var waitTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions
            {
                Timeout = Math.Max(_options.DefaultTimeoutMs, 60000)
            });
            await page.GetByText("Tek Tek PDF İndir", new PageGetByTextOptions { Exact = true }).First
                .ClickAsync();
            download = await waitTask;
        }
        catch (Exception ex)
        {
            await LogoutAsync(page, company);
            throw new InvalidOperationException(
                $"'{faturaNo}' için 'Tek Tek PDF İndir' indirmesi başlamadı/tamamlanmadı.", ex);
        }

        var tempZip = Path.Combine(Path.GetTempPath(),
            $"dp-pdf-{company.Id}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.zip");
        await download.SaveAsAsync(tempZip);
        _logger.LogInformation(
            "Firma {CompanyName}: PDF-ZIP indirildi → {Path} (öneri ad: {Name})",
            company.Name, tempZip, download.SuggestedFilename);

        try
        {
            var (fileName, pdfBytes) = ExtractSinglePdf(tempZip, faturaNo);
            await LogoutAsync(page, company);

            sw.Stop();
            _logger.LogInformation(
                "Firma {CompanyName}: {FaturaNo} PDF çıkarıldı ({Bytes} byte). Süre: {Ms}ms",
                company.Name, faturaNo, pdfBytes.Length, sw.ElapsedMilliseconds);

            return new InvoicePdfResult
            {
                FaturaNo = faturaNo,
                FileName = fileName,
                PdfBytes = pdfBytes
            };
        }
        finally
        {
            try { File.Delete(tempZip); } catch { /* yutuluyor — geçici dosya */ }
        }
    }

    // Grid satırlarını dolaşıp FaturaNo hücresi eşleşen ElementHandle'ı döner (yoksa null).
    private static async Task<IElementHandle?> FindInvoiceRowAsync(IPage page, string faturaNo)
    {
        var rows = await page.QuerySelectorAllAsync("tr[id^='InvoiceGrid_DXDataRow']");
        foreach (var row in rows)
        {
            var cells = await row.QuerySelectorAllAsync(":scope > td.dxgv");
            if (cells.Count > 2 &&
                string.Equals((await cells[2].InnerTextAsync()).Trim(), faturaNo, StringComparison.Ordinal))
                return row;
        }
        return null;
    }

    // İndirilen ZIP içinden tek PDF entry'sini çıkarır (ad + ham byte).
    private static (string fileName, byte[] pdfBytes) ExtractSinglePdf(string zipPath, string faturaNo)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.Entries.FirstOrDefault(e =>
                        e.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"'{faturaNo}' için indirilen ZIP içinde PDF bulunamadı " +
                        $"(entry sayısı: {zip.Entries.Count}).");

        using var es = entry.Open();
        using var ms = new MemoryStream();
        es.CopyTo(ms);
        return (entry.Name, ms.ToArray());
    }

    // ── Login ──────────────────────────────────────────────────────────────

    private async Task LoginWithRetryAsync(
        IPage page, Company company, string decryptedPassword, CancellationToken ct)
    {
        const int maxAttempts = 3;
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await AttemptLoginAsync(page, company, decryptedPassword);
                _logger.LogInformation("Firma {CompanyName}: Login başarılı (deneme {Attempt})",
                    company.Name, attempt);
                return;
            }
            catch (SovosCaptchaActiveException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning(
                    "Firma {CompanyName}: Login başarısız, deneme {Attempt}/{Max}. Hata: {Error}",
                    company.Name, attempt, maxAttempts, ex.Message);

                if (attempt < maxAttempts)
                    await Task.Delay(5000, ct);
            }
        }

        throw new SovosLoginException(
            $"{maxAttempts} deneme sonrası giriş başarısız - şifre veya kullanıcı adı yanlış olabilir.",
            lastEx!);
    }

    private async Task AttemptLoginAsync(IPage page, Company company, string decryptedPassword)
    {
        await page.GotoAsync(_options.PortalLoginUrl);

        if (await page.IsVisibleAsync("#divCaptcha"))
        {
            _logger.LogError("Firma {CompanyName}: Captcha aktif, manuel müdahale gerekli", company.Name);
            throw new SovosCaptchaActiveException();
        }

        await page.FillAsync("#txtCorporateCode", company.CompanyCode);
        await page.FillAsync("#txtLoginName", company.Username);
        await page.FillAsync("#txtLoginPassword", decryptedPassword);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Giriş" }).ClickAsync();

        if (await page.IsVisibleAsync("#divCaptcha"))
        {
            _logger.LogError("Firma {CompanyName}: Captcha aktif, manuel müdahale gerekli", company.Name);
            throw new SovosCaptchaActiveException();
        }

        try
        {
            await page.WaitForSelectorAsync(
                "text=Çıkış",
                new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 15000 });
        }
        catch (TimeoutException ex)
        {
            throw new SovosLoginException(
                "Login zaman aşımı (15 sn): 'Çıkış' linki görünmedi - giriş muhtemelen reddedildi.",
                ex);
        }
    }

    // ── Navigasyon: doğrudan Inbox.aspx ──────────────────────────────────

    private async Task NavigateToIncomingInvoicesAsync(IPage page, Company company)
    {
        if (string.IsNullOrWhiteSpace(_options.PortalIncomingInvoicesUrl))
        {
            _logger.LogWarning(
                "Firma {CompanyName}: Sovos:PortalIncomingInvoicesUrl yapılandırılmadı.",
                company.Name);
            return;
        }

        _logger.LogInformation("Firma {CompanyName}: Gelen Fatura sayfasına gidiliyor", company.Name);
        await page.GotoAsync(_options.PortalIncomingInvoicesUrl);

        await page.WaitForSelectorAsync(
            "#InvoiceGrid",
            new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = _options.DefaultTimeoutMs
            });

        _logger.LogInformation("Firma {CompanyName}: Gelen Fatura sayfası yüklendi", company.Name);
    }

    // ── Tarih filtresi + Sorgula (SovosScraper ile birebir) ──────────────

    private async Task ApplyDateFilterAsync(IPage page, Company company, DateTime fromDate, DateTime toDate)
    {
        var startStr = fromDate.ToString("dd.MM.yyyy", TrCulture);
        var endStr   = toDate.ToString("dd.MM.yyyy", TrCulture);

        _logger.LogInformation("Firma {CompanyName}: Tarih filtresi {Start} - {End}",
            company.Name, startStr, endStr);

        await page.FillAsync("#InvoiceFilterBeginDate_I", startStr);
        await page.Keyboard.PressAsync("Tab");
        await page.FillAsync("#InvoiceFilterEndDate_I", endStr);
        await page.Keyboard.PressAsync("Tab");

        // Sorgula — DevExpress ASPxButton: dış div'i hedefle (#btnRefresh).
        await page.Locator("#btnRefresh")
            .ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions { Timeout = 5000 });
        await page.ClickAsync("#btnRefresh");
        _logger.LogInformation("Firma {CompanyName}: Sorgula tıklandı", company.Name);

        await page.WaitForSelectorAsync(
            "#InvoiceGridLoadingPanel",
            new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 30000
            });
    }

    // ── XLSX indir + parse et ────────────────────────────────────────────

    private async Task<List<ScrapedInvoice>> DownloadAndParseExcelAsync(IPage page, Company company)
    {
        if (string.IsNullOrWhiteSpace(_options.ExcelExportSelector))
            throw new InvalidOperationException(
                "Sovos:ExcelExportSelector yapılandırılmadı. DP'deki XLSX indir butonunun selektörünü ekleyin.");

        _logger.LogInformation("Firma {CompanyName}: XLSX indir butonu tıklanıyor ({Sel})",
            company.Name, _options.ExcelExportSelector);

        IDownload download;
        try
        {
            var waitTask = page.WaitForDownloadAsync(new PageWaitForDownloadOptions
            {
                Timeout = Math.Max(_options.DefaultTimeoutMs, 60000)
            });
            // DevExpress ASPxButton: dış div'i hedefle (SovosScraper #btnRefresh pattern'i ile aynı).
            await page.Locator(_options.ExcelExportSelector)
                .ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions { Timeout = 5000 });
            await page.ClickAsync(_options.ExcelExportSelector);
            download = await waitTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"XLSX indirilemedi (selektör: {_options.ExcelExportSelector}). " +
                "Sovos:ExcelExportSelector ayarını DP UI'ına göre güncelleyin.",
                ex);
        }

        var tempPath = Path.Combine(Path.GetTempPath(),
            $"dp-gelen-{company.Id}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.xlsx");
        await download.SaveAsAsync(tempPath);

        _logger.LogInformation("Firma {CompanyName}: XLSX indirildi → {Path}", company.Name, tempPath);

        try
        {
            await using var stream = File.OpenRead(tempPath);
            var invoices = _excelParser.Parse(stream);

            _logger.LogInformation("Firma {CompanyName}: {Count} gelen fatura parse edildi",
                company.Name, invoices.Count);

            return invoices;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* yutuluyor — geçici dosya */ }
        }
    }

    // ── Logout ────────────────────────────────────────────────────────────

    private async Task LogoutAsync(IPage page, Company company)
    {
        try
        {
            await page.GetByText("Çıkış").ClickAsync();
            _logger.LogInformation("Firma {CompanyName}: Logout yapıldı", company.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Firma {CompanyName}: Logout başarısız (kritik değil): {Error}",
                company.Name, ex.Message);
        }
    }
}
