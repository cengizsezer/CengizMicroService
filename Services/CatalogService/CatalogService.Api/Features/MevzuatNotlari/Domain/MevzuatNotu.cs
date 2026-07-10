namespace CatalogService.Api.Features.MevzuatNotlari.Domain
{
    /// <summary>
    /// Bir mevzuat notu (ör. GVK/KVK/VUK madde açıklaması).
    /// Ulusal/ortak referans içeriktir; tenant'a bağlı DEĞİLDİR.
    /// </summary>
    public class MevzuatNotu
    {
        public int Id { get; set; }

        /// <summary>GVK/KVK/VUK/KDV/SGK/TTK</summary>
        public string Kategori { get; set; } = string.Empty;

        /// <summary>Örn: "Md. 328", "Mük. Md. 20/B".</summary>
        public string? MaddeNo { get; set; }

        public string Baslik { get; set; } = string.Empty;

        public string? Ozet { get; set; }

        /// <summary>Markdown içerik.</summary>
        public string? Icerik { get; set; }

        /// <summary>Virgülle ayrılmış etiketler.</summary>
        public string? Etiketler { get; set; }

        /// <summary>GİB linki / özelge no.</summary>
        public string? Kaynak { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
        public DateTime? GuncellemeTarihi { get; set; }
    }
}
