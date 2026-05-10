using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Sovos.InvoiceWorker.Configuration;
using Sovos.InvoiceWorker.Core.DTOs;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Exceptions;
using Sovos.InvoiceWorker.Core.Interfaces;

namespace Sovos.InvoiceWorker.Services;

public class SovosScraper : ISovosScraper
{
    private readonly SovosOptions _options;
    private readonly ILogger<SovosScraper> _logger;
    private static readonly CultureInfo TrCulture = new("tr-TR");

    public SovosScraper(IOptions<SovosOptions> options, ILogger<SovosScraper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<ScrapedInvoice>> FetchPendingInvoicesAsync(
        Company company,
        string decryptedPassword,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Firma {CompanyName}: Tarama başlıyor (aralık {From:dd.MM.yyyy} - {To:dd.MM.yyyy})",
            company.Name, fromDate, toDate);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.SlowMoMs
        });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(_options.DefaultTimeoutMs);

        await LoginWithRetryAsync(page, company, decryptedPassword, ct);
        await NavigateToPendingInvoicesAsync(page, company);
        var invoices = await ScrapeInvoicesAsync(page, company, fromDate, toDate);
        await LogoutAsync(page, company);

        sw.Stop();
        _logger.LogInformation(
            "Firma {CompanyName}: Tarama tamamlandı. {Count} fatura. Süre: {Ms}ms",
            company.Name, invoices.Count, sw.ElapsedMilliseconds);

        return invoices;
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

    // ── Navigasyon ────────────────────────────────────────────────────────

    private async Task NavigateToPendingInvoicesAsync(IPage page, Company company)
    {
        if (string.IsNullOrWhiteSpace(_options.PendingInvoicesUrl))
        {
            // TODO: Sol menüden E-Fatura → Gelen Kutusu → Onay Bekleyen tıkla.
            _logger.LogWarning(
                "Firma {CompanyName}: Sovos:PendingInvoicesUrl yapılandırılmadı. " +
                "Onay Bekleyen sayfasının URL'sini appsettings'e ekleyin.",
                company.Name);
            return;
        }

        _logger.LogInformation("Firma {CompanyName}: Onay Bekleyen sayfasına gidiliyor", company.Name);
        await page.GotoAsync(_options.PendingInvoicesUrl);

        await page.WaitForSelectorAsync(
            "#InvoiceGrid",
            new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = _options.DefaultTimeoutMs
            });

        _logger.LogInformation("Firma {CompanyName}: Onay Bekleyen sayfası yüklendi", company.Name);
    }

    // ── Scraping ──────────────────────────────────────────────────────────

    private async Task<List<ScrapedInvoice>> ScrapeInvoicesAsync(
        IPage page, Company company, DateTime fromDate, DateTime toDate)
    {
        var startStr = fromDate.ToString("dd.MM.yyyy", TrCulture);
        var endStr = toDate.ToString("dd.MM.yyyy", TrCulture);

        _logger.LogInformation("Firma {CompanyName}: Tarih filtresi {Start} - {End}",
            company.Name, startStr, endStr);

        await page.FillAsync("#InvoiceFilterBeginDate_I", startStr);
        await page.Keyboard.PressAsync("Tab");
        await page.FillAsync("#InvoiceFilterEndDate_I", endStr);
        await page.Keyboard.PressAsync("Tab");

        // Sorgula — DevExpress ASPxButton: tıklanabilir element dış div (#btnRefresh).
        // İçteki <input readonly> tıklanamaz. Genel kural: ASPxButton'larda dış div'i hedefle.
        await page.Locator("#btnRefresh")
            .ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions { Timeout = 5000 });
        await page.ClickAsync("#btnRefresh");
        _logger.LogInformation("Firma {CompanyName}: Sorgula tıklandı", company.Name);

        // Loading panel kaybolana kadar bekle (AJAX tamamlanmış olur)
        _logger.LogInformation("Firma {CompanyName}: Loading panel bekleniyor", company.Name);
        await page.WaitForSelectorAsync(
            "#InvoiceGridLoadingPanel",
            new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 30000
            });

        // Grid satırları
        var rows = await page.QuerySelectorAllAsync("tr[id^='InvoiceGrid_DXDataRow']");

        if (rows.Count == 0)
        {
            _logger.LogInformation("Firma {CompanyName}: Bu dönem için onay bekleyen fatura yok",
                company.Name);
            return new List<ScrapedInvoice>();
        }

        _logger.LogInformation("Firma {CompanyName}: {Count} satır bulundu, parse ediliyor...",
            company.Name, rows.Count);

        var invoices = new List<ScrapedInvoice>();

        bool firstRow = true;
        foreach (var row in rows)
        {
            try
            {
                // :scope > td.dxgv → sadece satırın DOĞRUDAN child td.dxgv'leri
                // (DevExpress nested editor table'larındaki td'ler dahil edilmez)
                var cells = await row.QuerySelectorAllAsync(":scope > td.dxgv");
                var texts = new List<string>();
                foreach (var cell in cells)
                    texts.Add((await cell.InnerTextAsync()).Trim());

                // İlk satırda hücre sayısını logla
                if (firstRow)
                {
                    _logger.LogInformation(
                        "Firma {CompanyName}: İlk satır — {Count} doğrudan veri hücresi",
                        company.Name, texts.Count);
                    firstRow = false;
                }

                // Beklenen yapı: index 0 = checkbox kolonu (boş), 1..12 = 12 spec kolonu,
                // 13+ = ekstra kolonlar (satış tipi, fatura tipi, durum, vb. — kullanılmıyor).
                // Yani toplam en az 13 hücre gerekli.
                if (texts.Count < 13)
                {
                    _logger.LogWarning(
                        "Firma {CompanyName}: Satırda {Count} hücre var, en az 13 bekleniyordu. " +
                        "Hücre içerikleri:",
                        company.Name, texts.Count);
                    for (int i = 0; i < texts.Count; i++)
                        _logger.LogWarning("  [{Index}] = '{Text}'", i, texts[i]);
                    continue;
                }

                // Sütun map'i (0-indexed; [0] checkbox, atla):
                //  [1]=FirmaUnvani [2]=FaturaNo [3]=GondericiVkn [4]=ParaBirimi
                //  [5]=FaturaTutari [6]=ToplamVergi [7]=IskontoTutari [8]=Artirim
                //  [9]=SiparisNo [10]=SonOdemeTarihi [11]=DuzenlenmeTarihi [12]=OlusturulmaTarihi
                var invoice = new ScrapedInvoice
                {
                    FirmaUnvani    = texts[1],
                    FaturaNo       = texts[2],
                    GondericiVkn   = texts[3],
                    ParaBirimi     = texts[4],
                    FaturaTutari   = ParseAmount(texts[5]),
                    ToplamVergi    = ParseAmount(texts[6]),
                    IskontoTutari  = ParseAmount(texts[7]),
                    Artirim        = ParseAmount(texts[8]),
                    SiparisNo      = string.IsNullOrWhiteSpace(texts[9]) ? null : texts[9],
                    SonOdemeTarihi    = ParseDate(texts[10]),
                    DuzenlenmeTarihi  = ParseDate(texts[11]),
                    OlusturulmaTarihi = ParseDate(texts[12])
                };

                invoices.Add(invoice);

                _logger.LogInformation(
                    "Fatura: {FaturaNo} | {FirmaUnvani} | {Tutar} {ParaBirimi}",
                    invoice.FaturaNo,
                    invoice.FirmaUnvani,
                    invoice.FaturaTutari.ToString("N2", TrCulture),
                    invoice.ParaBirimi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Firma {CompanyName}: Satır parse hatası: {Error}",
                    company.Name, ex.Message);
            }
        }

        _logger.LogInformation("Firma {CompanyName}: {Count} fatura bulundu",
            company.Name, invoices.Count);

        return invoices;
    }

    // ── Parse yardımcıları ────────────────────────────────────────────────

    private static decimal ParseAmount(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? 0m
            : decimal.Parse(raw.Trim(), NumberStyles.Number, TrCulture);

    private static DateTime? ParseDate(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? null
            : DateTime.ParseExact(
                raw.Trim(),
                new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy" },
                TrCulture,
                DateTimeStyles.None);

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
