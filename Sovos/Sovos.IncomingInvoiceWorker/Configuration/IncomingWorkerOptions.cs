namespace Sovos.IncomingInvoiceWorker.Configuration;

public class IncomingSovosOptions
{
    public string PortalLoginUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gelen Kutusu → "Fatura" sekmesinin direkt URL'i (Inbox.aspx). SovosScraper
    /// (onaylı fatura) ile aynı pattern: sekme/menü tıklama yok, doğrudan GotoAsync.
    /// </summary>
    public string PortalIncomingInvoicesUrl { get; set; } = string.Empty;

    /// <summary>
    /// Tarih filtresi uygulanıp grid yüklendikten sonra XLSX indirmek için
    /// tıklanacak DevExpress butonunun ID'si.
    /// </summary>
    public string ExcelExportSelector { get; set; } = "#exportToXLSXButton";

    public bool Headless { get; set; } = true;
    public int SlowMoMs { get; set; } = 0;
    public int DefaultTimeoutMs { get; set; } = 30000;
}
