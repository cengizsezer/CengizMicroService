namespace Sovos.InvoiceWorker.Configuration;

public class WorkerOptions
{
    public int IntervalMinutes { get; set; } = 60;
    public int InitialDelaySeconds { get; set; } = 30;
}

public class SovosOptions
{
    public string PortalLoginUrl { get; set; } = string.Empty;
    /// <summary>
    /// Onay Bekleyen sayfasının tam URL'si.
    /// İlk çalıştırmada boş bırakın: login test edilir, ana sayfada kalınır.
    /// URL'yi tespit ettikten sonra buraya ekleyin.
    /// </summary>
    public string PendingInvoicesUrl { get; set; } = string.Empty;
    public bool Headless { get; set; } = true;
    public int SlowMoMs { get; set; } = 0;
    public int DefaultTimeoutMs { get; set; } = 30000;
    /// <summary>
    /// true iken uygulama başladığında TestCompany ile scraper'ı çalıştırır, worker başlamaz.
    /// </summary>
    public bool TestScraperOnStartup { get; set; } = false;
}

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
}

public class BrevoOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
}
