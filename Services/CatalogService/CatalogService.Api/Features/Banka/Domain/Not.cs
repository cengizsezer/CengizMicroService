namespace CatalogService.Api.Features.Banka.Domain
{
    /// <summary>
    /// Bir hesaba ait serbest metin notu. Kapsam'a göre bir güne, bir aya
    /// veya hesaba genel olarak bağlanır. Sabit notlar pinli/devir notudur.
    /// </summary>
    public class Not
    {
        public int Id { get; set; }

        // Hesap'a FK (Cascade).
        public int HesapId { get; set; }

        public NotKapsam Kapsam { get; set; }

        // Kapsam=Gun ise dolu (gün).
        public DateTime? Tarih { get; set; }

        // Kapsam=Ay ise Yil + Ay dolu.
        public int? Yil { get; set; }
        public int? Ay { get; set; }

        public string Metin { get; set; } = string.Empty;

        // Pinli / devir notu — listede en üstte ve vurgulu gösterilir.
        public bool Sabit { get; set; }

        public string? OlusturanKullanici { get; set; }
        public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    }
}
