namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>
    /// Mevcut bir notun Id üzerinden güncellenmesi. Upsert'ten farkı: notun tipi
    /// (kalıcı ↔ dönem notu, yani <see cref="DonemYili"/>) burada değişebilir.
    /// Tip anahtarın parçası olduğundan upsert ile değiştirilemez — bu uç nokta
    /// kaydı yerinde taşır, kullanıcı silip yeniden yazmak zorunda kalmaz.
    /// Hesap kodu değişmez; not başka hesaba taşınmaz.
    /// </summary>
    public class MizanNotuGuncelleDto
    {
        public string Metin { get; set; } = string.Empty;

        /// <summary>0=Açıklama, 1=Düzeltilecek.</summary>
        public int NotTuru { get; set; }

        /// <summary>null = kalıcı not.</summary>
        public int? DonemYili { get; set; }

        public bool UyariBastir { get; set; }
    }
}
