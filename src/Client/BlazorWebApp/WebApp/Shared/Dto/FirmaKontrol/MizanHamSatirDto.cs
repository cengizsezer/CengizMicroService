namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Client-side parse edilmiş tek ham mizan satırı (kod + ad + bakiye).</summary>
    public class MizanHamSatirDto
    {
        public string Kod { get; set; } = string.Empty;
        public string? Ad { get; set; }
        public decimal? Bakiye { get; set; }
    }
}
