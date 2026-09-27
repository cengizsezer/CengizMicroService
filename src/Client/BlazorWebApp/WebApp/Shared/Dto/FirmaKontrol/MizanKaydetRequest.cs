namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Bir dönemin (Onceki/Cari) ham mizanını kaydetme isteği (idempotent).</summary>
    public class MizanKaydetRequest
    {
        public int Donem { get; set; }
        public int Yil { get; set; }
        public List<MizanHamSatirDto> Satirlar { get; set; } = new();

        /// <summary>
        /// Aynı yüklemenin hesap kırılım ağacı; ham satırların yanında, aynı
        /// (FirmaId, Donem, Yil) anahtarıyla saklanır. Hiçbir toplamda kullanılmaz.
        /// </summary>
        public List<MizanHamDugumDto> Dugumler { get; set; } = new();
    }
}
