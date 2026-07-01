using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Firma Kontrol / Raporlar → Mizan sekmesinin HAM satırları. Firma + dönem
    /// (Onceki/Cari) + yıl bazında saklanır. Dikey analiz, oranlar, gelir tablosu
    /// özetleri bu ham değerlerden runtime'da hesaplanır — hesaplanmışlar SAKLANMAZ.
    /// Yükleme idempotenttir: (FirmaId, Donem, Yil) için sil + yeniden yaz.
    /// </summary>
    public class FirmaKontrolMizanSatir
    {
        public long Id { get; set; }

        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        /// <summary>Donem enum: 0=Onceki, 1=Cari (frontend WebApp.Domain...Donem ile aynı).</summary>
        public int Donem { get; set; }

        /// <summary>Hesap dönemi yılı. Şimdilik cari yıl yazılır; yıl bazlı geçmiş için ucu açık.</summary>
        public int Yil { get; set; }

        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;

        public decimal? Bakiye { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
