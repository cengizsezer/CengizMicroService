using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.KdvBeyanname.Domain
{
    public class KdvBeyannameYevmiye
    {
        public long Id { get; set; }
        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        // YYYY-MM (örn: "2026-04")
        public string Donem { get; set; } = string.Empty;

        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;

        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }

        public string? FisNo { get; set; }
        public DateTime? Tarih { get; set; }
        public string? Aciklama { get; set; }

        // Yevmiye satırından çıkarılan fatura numarası (J+K kolonlarının birleşimi)
        public string? FaturaNo { get; set; }

        // Filtreleme için
        public string? BelgeTipi { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
