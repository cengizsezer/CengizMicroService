namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>
    /// Hesap kırılım ağacının tek düğümü — ana hesap ("600") ya da alt kırılım ("600 1 21").
    ///
    /// Seviye / üst kod / ana hesap kodu SAKLANMAZ; koddan türetilir (sabit karakter
    /// genişliği varsayılmaz, parçalar harf içerebilir: "120 1 A08").
    ///
    /// DİKKAT: Bu düğümler hiçbir toplama girmez; ham mizan değerleri (üç haneli kodlar)
    /// <see cref="MizanHamSatirDto"/> ile ayrı saklanır ve oranlar oradan hesaplanır.
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
