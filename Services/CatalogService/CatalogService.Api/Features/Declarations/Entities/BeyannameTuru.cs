namespace CatalogService.Api.Features.Declarations.Entities
{
    /// <summary>
    /// Beyanname türü tanımı: vergi kodu + okunur ad. Özet matrisinin kolonları ve
    /// Takip ekranının tür listesi <b>bu tablodan</b> gelir.
    ///
    /// <b>Neden tablo?</b> Liste bugüne kadar <c>DeclarationFollow.razor</c> içinde
    /// <c>List&lt;string&gt;</c> olarak duruyordu ("0015 KDV-1", "SGK" …). Matris
    /// kolonlarını da oradan almak, aynı listenin iki ekranda ayrı ayrı yaşamasına ve
    /// yeni bir tür eklemek için kod değiştirmeye yol açardı.
    ///
    /// Tablo <b>global</b>: beyanname türleri ülke çapında aynı, firmadan ve tenant'tan
    /// bağımsız.
    /// </summary>
    public class BeyannameTuru
    {
        public int Id { get; set; }

        /// <summary>
        /// <see cref="Declaration.DeclarationType"/> alanında <b>saklanan</b> metin,
        /// ör. <c>"0015 KDV-1"</c>. Mevcut kayıtlar bu metinle yazıldığı için tanım
        /// tablosu da onu taşır; eşleştirme buradan yapılır (bkz. <see cref="Services.BeyannameTuruEsleyici"/>).
        /// </summary>
        public string Deger { get; set; } = string.Empty;

        /// <summary>Vergi kodu, ör. <c>"0015"</c>. Kolon başlığının altında gösterilir; SGK gibi kodsuz türlerde boş olabilir.</summary>
        public string? Kod { get; set; }

        /// <summary>Ekranda görünen ad, ör. <c>"KDV (1 No.lu)"</c>.</summary>
        public string Ad { get; set; } = string.Empty;

        /// <summary>Matris kolonlarının ve listelerin sırası.</summary>
        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
