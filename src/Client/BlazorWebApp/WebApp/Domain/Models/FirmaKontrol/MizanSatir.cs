namespace WebApp.Domain.Models.FirmaKontrol
{
    public class MizanSatir
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public SatirTipi Tip { get; set; }
        public decimal? OncekiDonem { get; set; }
        public decimal? CariDonem { get; set; }
    }
}
