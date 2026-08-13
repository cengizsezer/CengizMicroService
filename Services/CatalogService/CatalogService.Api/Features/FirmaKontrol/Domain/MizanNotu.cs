using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Firma Kontrol / Raporlar → Mizan sekmesindeki bir hesap satırına iliştirilen
    /// serbest not. "Bu bakiye neden böyle?" gerekçesini kalıcı olarak saklar
    /// (örn. 381 için "X firmasının Aralık faturası Ocak'ta geldiği için tahakkuk
    /// ettirildi").
    ///
    /// İki tür not vardır:
    ///  • Kalıcı not (<see cref="DonemYili"/> = null) — her hesap döneminde görünür.
    ///  • Dönem notu (<see cref="DonemYili"/> = 2026 gibi) — yalnızca o yılda görünür.
    ///
    /// (FirmaId, HesapKodu, DonemYili) tekildir. SQL Server'da unique index NULL'ları
    /// eşit saydığından bir hesabın EN FAZLA bir kalıcı notu, artı yıl başına bir
    /// dönem notu olur — yazma işlemi bu yüzden upsert'tür.
    /// </summary>
    public class MizanNotu
    {
        public long Id { get; set; }

        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        /// <summary>
        /// Notun bağlandığı hesap kodu. Mizan ekranı 3 haneli ana hesap satırlarını
        /// gösterir ("381"), ancak alt kırılım de yazılabilir ("381.01") — parser alt
        /// kodları mizana yazmadığından böyle bir not UI'da ana hesabın satırında
        /// gruplanır. Bkz. <see cref="AnaHesapKodu"/>.
        /// </summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string Metin { get; set; } = string.Empty;

        /// <summary>
        /// Notun türü — 0=Açıklama, 1=Düzeltilecek. "Düzeltilecek" notlar bir iş
        /// kaydıdır: bakiye hiç değişmediyse UI "iş yapılmamış" sinyali gösterir.
        /// (Kalıcı/dönem ayrımı tür DEĞİL kapsamdır — bkz. <see cref="DonemYili"/>.)
        /// </summary>
        public int NotTuru { get; set; }

        /// <summary>Hesap dönemi yılı. null = kalıcı not (her dönemde görünür).</summary>
        public int? DonemYili { get; set; }

        /// <summary>
        /// true ise bu hesabın otomatik uyarısı Kritik/Uyarı sayaçlarından çıkarılıp
        /// "Notla Açıklanmış" sayacına alınır — uyarı silinmez, sadece gruplanır.
        /// </summary>
        public bool UyariBastir { get; set; }

        // ── Bakiye snapshot'ı ───────────────────────────────────────────────
        // Not yazıldığı/düzenlendiği anda hesabın mizandaki değeri. Amaç notun
        // "bayatlığını" görebilmek: mizan yeniden yüklenip tutar değişince not hâlâ
        // eski duruma göre yazılmış olur. Otomatik silme YOK — sadece sinyal.
        // Mizanda karşılığı olmayan kod (alt kırılım vb.) için hepsi null kalır.

        /// <summary>
        /// AYRILMIŞ: mizan hattı bugün borç/alacağı ayrı saklamıyor
        /// (ExcelMizanParser bakiyeye indirger), bu yüzden şimdilik hep null.
        /// </summary>
        public decimal? SnapshotBorc { get; set; }

        /// <summary>AYRILMIŞ — bkz. <see cref="SnapshotBorc"/>.</summary>
        public decimal? SnapshotAlacak { get; set; }

        /// <summary>Snapshot anındaki mizan bakiyesi. Karşılaştırma bunun üzerinden yapılır.</summary>
        public decimal? SnapshotBakiye { get; set; }

        /// <summary>Snapshot'ın alındığı an. Bakiye bulunamadıysa null.</summary>
        public DateTime? SnapshotTarihi { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Hesap kodunun 3 haneli ana hesap karşılığı ("381.01" → "381"). Mizan
        /// satırları ana hesap bazlı olduğundan not→satır eşleştirmesi bunun
        /// üzerinden yapılır. 3 haneli sayısal önek yakalanamazsa kod olduğu gibi
        /// döner (beklenmedik format sessizce kaybolmasın).
        /// </summary>
        public static string AnaHesapKodu(string? hesapKodu)
        {
            if (string.IsNullOrWhiteSpace(hesapKodu)) return string.Empty;

            var kod = hesapKodu.Trim();

            var basamak = 0;
            while (basamak < kod.Length && basamak < 3 && char.IsDigit(kod[basamak]))
                basamak++;

            return basamak == 3 ? kod[..3] : kod;
        }
    }
}
