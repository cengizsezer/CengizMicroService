namespace CatalogService.Api.Features.MevzuatGrafigi.Domain
{
    /// <summary>
    /// İki kart arasındaki yönlü bağlantı (kaynak → hedef). Sıra kuralı <see cref="BaglantiKurali"/>'nda.
    /// (KaynakKartId, HedefKartId) benzersizdir.
    /// </summary>
    public class MevzuatBaglanti
    {
        public int Id { get; set; }
        public int KaynakKartId { get; set; }
        public int HedefKartId { get; set; }
    }
}
