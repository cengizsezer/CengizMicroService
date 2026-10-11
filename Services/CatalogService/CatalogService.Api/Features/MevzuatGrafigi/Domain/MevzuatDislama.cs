namespace CatalogService.Api.Features.MevzuatGrafigi.Domain
{
    /// <summary>
    /// İki kartın birlikte uygulanamayacağı bilgisi (ör. aynı kazanç için iki teşvik).
    /// Yönsüzdür: A-B ile B-A aynı kayıttır. Bunu garanti etmek için çift her zaman
    /// AKartId &lt; BKartId olacak şekilde sıralı saklanır.
    /// </summary>
    public class MevzuatDislama
    {
        public int Id { get; set; }
        public int AKartId { get; set; }
        public int BKartId { get; set; }
        public string? Not { get; set; }
    }
}
