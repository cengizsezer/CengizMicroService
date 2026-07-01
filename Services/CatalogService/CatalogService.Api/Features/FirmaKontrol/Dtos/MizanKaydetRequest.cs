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
    }
}
