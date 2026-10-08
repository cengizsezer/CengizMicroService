namespace WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model
{
    /// <summary>Borç verenin vergisel tipi; faiz stopajını belirler (KVK 12/7).</summary>
    public enum BorcVerenTipi
    {
        /// <summary>Tam mükellef kurum: örtülü kâr payı stopaja konu değil.</summary>
        TamMukellefKurum,

        /// <summary>Gerçek kişi veya dar mükellef: örtülü faiz üzerinden kâr payı stopajı.</summary>
        GercekKisiVeyaDarMukellef
    }

    /// <summary>Ekranın üst panelindeki parametreler.</summary>
    public class OrtuluSermayeParametre
    {
        /// <summary>Beyanname tablosundaki tarih; zirve bulunamazsa gösterilir.</summary>
        public DateTime DonemSonu { get; set; }

        /// <summary>Dönem başı öz sermaye (VUK).</summary>
        public decimal DonemBasiOzSermaye { get; set; }

        /// <summary>
        /// İlişkili banka / finans kurumu borcu (KVK 12/3): borcun yarısı dikkate alınır,
        /// fiilen 6 kat sınırı.
        /// </summary>
        public bool IliskiliBankaMi { get; set; }

        public BorcVerenTipi BorcVerenTipi { get; set; } = BorcVerenTipi.TamMukellefKurum;

        /// <summary>Kâr payı stopaj oranı, oran olarak (0,15 = %15).</summary>
        public decimal StopajOrani { get; set; } = 0.15m;
    }
}
