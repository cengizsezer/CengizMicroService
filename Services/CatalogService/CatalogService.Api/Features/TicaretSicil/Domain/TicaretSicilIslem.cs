namespace CatalogService.Api.Features.TicaretSicil.Domain
{
    /// <summary>
    /// Bir ticaret sicil işlemi (ör. Anonim Şirket Kuruluşu).
    /// Ulusal/ortak referans içeriktir; tenant'a bağlı DEĞİLDİR.
    /// </summary>
    public class TicaretSicilIslem
    {
        public int Id { get; set; }

        /// <summary>URL slug (benzersiz). Örn: "anonim".</summary>
        public string Slug { get; set; } = string.Empty;

        public string Kategori { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;

        /// <summary>Listede gösterim sırası.</summary>
        public int Sira { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<TicaretSicilAdim> Adimlar { get; set; } = new List<TicaretSicilAdim>();
    }
}
