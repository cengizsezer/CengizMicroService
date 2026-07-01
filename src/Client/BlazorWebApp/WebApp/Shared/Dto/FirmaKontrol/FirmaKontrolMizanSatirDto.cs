namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>DB'de saklı ham mizan satırı (okuma; CatalogService'ten gelir).</summary>
    public class FirmaKontrolMizanSatirDto
    {
        public int Donem { get; set; }
        public int Yil { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public decimal? Bakiye { get; set; }
    }
}
