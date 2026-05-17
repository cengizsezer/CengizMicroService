using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.KdvBeyanname.Domain
{
    public class GelenFatura
    {
        public long Id { get; set; }
        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        public string FaturaNo { get; set; } = string.Empty;
        public string GondericiVkn { get; set; } = string.Empty;
        public string GondericiUnvan { get; set; } = string.Empty;

        public DateTime? FaturaTarihi { get; set; }
        public string ParaBirimi { get; set; } = string.Empty;

        public decimal MatrahTutari { get; set; }
        public decimal KdvTutari { get; set; }
        public decimal ToplamTutar { get; set; }
        public decimal? KdvOrani { get; set; }

        // DP "Statü" kolonundan gelir (örn. SİSTEME KAYDEDİLDİ, FATURA KABUL, FATURA RED).
        // Upsert'te ToUpperInvariant().Trim() ile saklanır; karşılaştırma RED'leri eler.
        public string? Statu { get; set; }

        public DateTime SonGuncelleme { get; set; } = DateTime.UtcNow;
        public string Kaynak { get; set; } = "DP";
    }
}
