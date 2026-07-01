namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Bir dönemin (Onceki/Cari) ham mizanını kaydetme isteği (idempotent).</summary>
    public class MizanKaydetRequest
    {
        public int Donem { get; set; }
        public int Yil { get; set; }
        public List<MizanHamSatirDto> Satirlar { get; set; } = new();
    }
}
