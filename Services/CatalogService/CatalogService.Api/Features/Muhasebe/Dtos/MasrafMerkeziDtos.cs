namespace CatalogService.Api.Features.Muhasebe.Dtos
{
    /// <summary>Masraf merkezi kaydı. Firma (tenant) bazlıdır; fiş satırında seçilir.</summary>
    public class MasrafMerkeziDto
    {
        public int MasrafMerkeziId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; }
    }

    /// <summary>Yeni masraf merkezi. Kod firma içinde tekildir.</summary>
    public class MasrafMerkeziYazDto
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
    }
}
