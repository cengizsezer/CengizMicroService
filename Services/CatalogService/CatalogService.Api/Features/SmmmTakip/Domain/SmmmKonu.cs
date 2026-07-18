namespace CatalogService.Api.Features.SmmmTakip.Domain
{
    /// <summary>
    /// SMMM Takip konu ağacının bir düğümü (self-referans hiyerarşi).
    /// Ortak referans içeriktir; tenant'a bağlı DEĞİLDİR.
    /// </summary>
    public class SmmmKonu
    {
        public int Id { get; set; }

        /// <summary>Üst konu (kök ise null).</summary>
        public int? UstKonuId { get; set; }

        public string Baslik { get; set; } = string.Empty;

        /// <summary>URL slug (benzersiz).</summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>Markdown içerik (opsiyonel).</summary>
        public string? IcerikMd { get; set; }

        /// <summary>Kardeşler arası gösterim sırası.</summary>
        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;

        public DateTime? GuncellenmeTarihi { get; set; }

        public ICollection<SmmmKonu> AltKonular { get; set; } = new List<SmmmKonu>();
        public ICollection<SmmmHad> Hadler { get; set; } = new List<SmmmHad>();
    }
}
