namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>
    /// Bir dönemin (Onceki/Cari) ham mizanını kaydetme isteği. Idempotent:
    /// (FirmaId, Donem, Yil) için mevcut satırlar silinip bunlar yazılır.
    /// </summary>
    public class MizanKaydetRequest
    {
        public int Donem { get; set; }
        public int Yil { get; set; }
        public List<MizanHamSatirDto> Satirlar { get; set; } = new();

        /// <summary>
        /// Aynı yüklemenin hesap kırılım ağacı (ana hesaplar + alt kırılımlar). Ham
        /// satırların yanında, aynı (FirmaId, Donem, Yil) anahtarıyla saklanır.
        /// Boş gelirse o dönemin ağacı silinir — eski istemciler bu alanı göndermez.
        /// Bu düğümler hiçbir toplamda kullanılmaz.
        /// </summary>
        public List<MizanHamDugumDto> Dugumler { get; set; } = new();
    }
}
