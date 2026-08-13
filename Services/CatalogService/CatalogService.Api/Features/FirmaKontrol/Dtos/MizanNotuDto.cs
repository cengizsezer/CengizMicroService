namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>Mizan hesap notu (okuma).</summary>
    public class MizanNotuDto
    {
        public long Id { get; set; }

        /// <summary>Notun yazıldığı kod — alt kırılım olabilir ("381.01").</summary>
        public string HesapKodu { get; set; } = string.Empty;

        /// <summary>
        /// Eşleştirmede kullanılacak 3 haneli ana hesap ("381"). Sunucuda türetilir ki
        /// UI aynı kuralı tekrar yazmak zorunda kalmasın.
        /// </summary>
        public string AnaHesapKodu { get; set; } = string.Empty;

        public string Metin { get; set; } = string.Empty;

        /// <summary>0=Açıklama, 1=Düzeltilecek.</summary>
        public int NotTuru { get; set; }

        /// <summary>null = kalıcı not.</summary>
        public int? DonemYili { get; set; }

        public bool UyariBastir { get; set; }

        // Not yazıldığı/düzenlendiği andaki mizan değeri — UI bunu güncel bakiyeyle
        // karşılaştırıp notun bayatlığını gösterir. Mizanda karşılığı yoksa null.
        public decimal? SnapshotBorc { get; set; }
        public decimal? SnapshotAlacak { get; set; }
        public decimal? SnapshotBakiye { get; set; }
        public DateTime? SnapshotTarihi { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
