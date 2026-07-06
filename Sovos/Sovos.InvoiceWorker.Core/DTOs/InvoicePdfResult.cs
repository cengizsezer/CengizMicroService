namespace Sovos.InvoiceWorker.Core.DTOs;

/// <summary>
/// Tek bir faturanın DP portalından canlı çekilen PDF'i. Portal "Tek Tek PDF İndir"
/// aksiyonu PDF'i bir ZIP içinde verdiği için, ZIP açılıp içindeki tek PDF entry
/// çıkarılır; <see cref="PdfBytes"/> ham PDF içeriğidir (ZIP değil).
/// </summary>
public class InvoicePdfResult
{
    public string FaturaNo { get; init; } = string.Empty;

    /// <summary>ZIP içindeki PDF dosyasının adı (örn. "YKB2026001133881-...pdf").</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Ham PDF byte'ları (%PDF-...).</summary>
    public byte[] PdfBytes { get; init; } = Array.Empty<byte>();

    public string ContentType => "application/pdf";
}
