using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.KdvBeyanname.Domain
{
    public class KdvBeyannameMizan
    {
        public long Id { get; set; }
        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        // YYYY-MM
        public string Donem { get; set; } = string.Empty;

        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;

        public decimal BorcToplam { get; set; }
        public decimal AlacakToplam { get; set; }
        public decimal BorcKalan { get; set; }
        public decimal AlacakKalan { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
