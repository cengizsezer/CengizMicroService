using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Mizanın hesap kırılım ağacı — ham mizan satırlarının YANINDA, aynı
    /// (FirmaId, Donem, Yil) anahtarıyla saklanır. Ham satırlarla aynı istekte,
    /// aynı serviste, aynı idempotent akışta yazılır.
    ///
    /// Düğümler TEK BİR JSON listesi olarak tutulur (satır başına kayıt değil):
    /// erişim deseni her zaman "şu firma + dönem için ağacın TAMAMI"dır — düğüm
    /// bazında sorgu, join ya da toplama YOKTUR ve kural gereği hiç olmayacaktır.
    /// Böylece birkaç bin düğümlü bir yükleme, binlerce satır yerine tek sil+yaz
    /// ile döner ve alt kırılımlar ham değer tablosuna fiziksel olarak karışamaz.
    ///
    /// DİKKAT: Bu düğümler hiçbir toplamda kullanılmaz. Bilanço, Gelir Tablosu,
    /// Vergi, Dikey Yüzdeler ve Finansal Oranlar yalnızca üç haneli ana hesap
    /// kodlarından (FirmaKontrolMizanSatir) hesaplanmaya devam eder.
    /// </summary>
    public class FirmaKontrolMizanAgac
    {
        public long Id { get; set; }

        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        /// <summary>Donem enum: 0=Onceki, 1=Cari (ham mizan satırlarıyla aynı).</summary>
        public int Donem { get; set; }

        public int Yil { get; set; }

        /// <summary>
        /// <c>MizanHamDugumDto</c> listesinin JSON hâli. Seviye / üst kod / ana hesap
        /// kodu SAKLANMAZ — koddan türetilir (sabit karakter genişliği varsayılmaz).
        /// </summary>
        public string DugumlerJson { get; set; } = "[]";

        /// <summary>Teşhis ve hızlı sayım için; JSON'u açmadan kaç düğüm olduğu bilinsin.</summary>
        public int DugumSayisi { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // ---- Yükleme özeti (anasayfa "mizan durumu") ----
        // Kayıt sırasında bir kez yazılır; okuyan ekran JSON'u açmaz, hesap yapmaz.
        // Bu alanlar gelmeden önce yüklenmiş kayıtlarda BOŞ kalır; ekran "—" gösterir.

        /// <summary>En derin düğümün seviyesi (ana hesap = 1). Boşsa bilinmiyor.</summary>
        public int? SeviyeSayisi { get; set; }

        /// <summary>Ana hesap düğümlerinin borç bakiyeleri toplamı. Boşsa bilinmiyor.</summary>
        public decimal? BorcToplam { get; set; }

        /// <summary>Ana hesap düğümlerinin alacak bakiyeleri toplamı. Boşsa bilinmiyor.</summary>
        public decimal? AlacakToplam { get; set; }
    }
}
