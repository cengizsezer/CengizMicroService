namespace Sovos.IncomingInvoiceWorker.Services;

public interface IInvoicePdfService
{
    /// <summary>
    /// Faturanın PDF'ini döndürür. (FirmaId, FaturaNo) için daha önce çekilmiş bir
    /// FileId varsa Sovos'a hiç gitmeden onu döndürür (login maliyetinden kaçınır);
    /// yoksa firma kilidi altında portaldan çeker, FileApiService'e yükler, eşlemeyi
    /// kaydeder ve yeni FileId'yi döndürür.
    /// </summary>
    Task<FaturaPdfDto> GetOrFetchAsync(
        int firmaId, string faturaNo, int yil, int ay, CancellationToken ct);
}

public class FaturaPdfDto
{
    public string FaturaNo { get; set; } = string.Empty;
    public int FileId { get; set; }
    public string? FileName { get; set; }

    /// <summary>true = kayıt zaten vardı, scrape yapılmadı.</summary>
    public bool Cached { get; set; }
}
