namespace Sovos.InvoiceWorker.Core.Entities;

public class Invoice
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public string FaturaNo { get; set; } = string.Empty;
    public string GondericiVkn { get; set; } = string.Empty;
    public string FirmaUnvani { get; set; } = string.Empty;
    public string ParaBirimi { get; set; } = string.Empty;
    public decimal FaturaTutari { get; set; }
    public decimal ToplamVergi { get; set; }
    public decimal IskontoTutari { get; set; }
    public decimal Artirim { get; set; }
    public string? SiparisNo { get; set; }
    public DateTime? SonOdemeTarihi { get; set; }
    public DateTime? DuzenlenmeTarihi { get; set; }
    public DateTime? OlusturulmaTarihi { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime? NotifiedAt { get; set; }

    public Company Company { get; set; } = null!;
}
