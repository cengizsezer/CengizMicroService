namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Mizan hesap notu (okuma; CatalogService'ten gelir).</summary>
    public class MizanNotuDto
    {
        public long Id { get; set; }

        /// <summary>Notun yazıldığı kod — alt kırılım olabilir ("381.01").</summary>
        public string HesapKodu { get; set; } = string.Empty;

        /// <summary>
        /// Mizan satırıyla eşleştirmede kullanılacak 3 haneli ana hesap ("381").
        /// Sunucuda türetilir; UI aynı kuralı tekrar yazmaz.
        /// </summary>
        public string AnaHesapKodu { get; set; } = string.Empty;

        public string Metin { get; set; } = string.Empty;

        /// <summary>0=Açıklama, 1=Düzeltilecek (bkz. MizanNotTuru).</summary>
        public int NotTuru { get; set; }

        /// <summary>null = kalıcı not (her dönemde görünür).</summary>
        public int? DonemYili { get; set; }

        /// <summary>true ise hesabın uyarısı "Notla Açıklanmış" sayacına alınır.</summary>
        public bool UyariBastir { get; set; }

        // Not yazıldığı/düzenlendiği andaki mizan değeri. Güncel bakiyeyle
        // karşılaştırılarak notun bayatlığı gösterilir; mizanda karşılığı yoksa null.

        /// <summary>AYRILMIŞ: mizan hattı borç/alacağı ayrı saklamadığından şimdilik hep null.</summary>
        public decimal? SnapshotBorc { get; set; }

        /// <summary>AYRILMIŞ — bkz. <see cref="SnapshotBorc"/>.</summary>
        public decimal? SnapshotAlacak { get; set; }

        public decimal? SnapshotBakiye { get; set; }
        public DateTime? SnapshotTarihi { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
