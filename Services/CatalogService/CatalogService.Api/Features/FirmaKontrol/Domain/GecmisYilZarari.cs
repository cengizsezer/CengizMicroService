namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Devreden geçmiş yıl zararı. Mahsup en eski yıldan başlar ve 5 hesap döneminden
    /// eski zararlar mahsup edilemez (KVK 9/1-a); sınır hesaplama motorunda uygulanır.
    /// </summary>
    public class GecmisYilZarari
    {
        public int Id { get; set; }

        public int HesaplamaId { get; set; }
        public VergiHesaplama? Hesaplama { get; set; }

        /// <summary>Zararın doğduğu hesap dönemi.</summary>
        public short ZararYili { get; set; }

        /// <summary>Devreden zarar tutarı (pozitif girilir).</summary>
        public decimal ZararTutari { get; set; }

        /// <summary>
        /// Bu dönemde mahsup edilen kısım. Motor tarafından hesaplanır ve kaydedilirken
        /// yazılır; kullanıcı doğrudan girmez.
        /// </summary>
        public decimal MahsupEdilen { get; set; }
    }
}
