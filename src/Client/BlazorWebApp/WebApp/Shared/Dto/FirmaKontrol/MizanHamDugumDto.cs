namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>
    /// Hesap kırılım ağacının tek düğümü (tel üzerinde). Seviye / üst kod / ana hesap kodu
    /// taşınmaz — koddan türetilir, sabit karakter genişliği varsayılmaz.
    /// Bu düğümler hiçbir toplamda kullanılmaz.
    /// </summary>
    public class MizanHamDugumDto
    {
        public string Kod { get; set; } = string.Empty;
        public string? Ad { get; set; }
        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }
        public decimal Bakiye { get; set; }
    }
}
