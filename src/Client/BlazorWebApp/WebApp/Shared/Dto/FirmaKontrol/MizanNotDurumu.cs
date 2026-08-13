namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>
    /// Bir notun snapshot'ı ile mizandaki güncel bakiyenin karşılaştırma sonucu.
    /// MizanTab notları indekslerken not başına BİR KEZ hesaplar; kartlar yalnızca
    /// okur — render döngüsünde karşılaştırma yapılmaz.
    ///
    /// Snapshot yoksa (mizanda karşılığı olmayan kod) her iki bayrak da false'tur;
    /// bu durumda hiçbir sinyal gösterilmez.
    /// </summary>
    public class MizanNotDurumu
    {
        /// <summary>Notun hesabının mizandaki güncel cari bakiyesi.</summary>
        public decimal? GuncelBakiye { get; init; }

        /// <summary>Snapshot var ve güncel bakiyeden farklı — not bayatlamış olabilir.</summary>
        public bool Bayat { get; init; }

        /// <summary>
        /// Snapshot var ve güncel bakiyeyle birebir aynı. "Düzeltilecek" türündeki
        /// notlarda "iş yapılmamış" sinyali olarak kullanılır.
        /// </summary>
        public bool Degismedi { get; init; }
    }
}
