namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>
    /// Mevcut bir notun Id üzerinden güncellenmesi. Upsert'ten farkı: notun tipi
    /// (kalıcı ↔ dönem notu, yani <see cref="DonemYili"/>) burada değişebilir.
    /// Hesap kodu değişmez.
    /// </summary>
    public class MizanNotuGuncelleDto
    {
        public string Metin { get; set; } = string.Empty;

        /// <summary>0=Açıklama, 1=Düzeltilecek (bkz. MizanNotTuru).</summary>
        public int NotTuru { get; set; }

        /// <summary>null = kalıcı not.</summary>
        public int? DonemYili { get; set; }

        public bool UyariBastir { get; set; }
    }
}
