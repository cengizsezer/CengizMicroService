namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi.Domain
{
    /// <summary>
    /// KVK 11/1-i (ve GVK 41/9) uyarınca aşan kısma isabet eden finansman giderinin
    /// KKEG yazılacak yüzdesi. Oranı Cumhurbaşkanı Kararı belirliyor ve değişebiliyor;
    /// bu yüzden koda gömülmeyip yıl bazında bu tabloda tutuluyor (bkz. KARARLAR §80).
    ///
    /// İçerik firmadan/tenant'tan bağımsız ortak referanstır — mevzuat oranı herkes için
    /// aynıdır (SmmmHadDegeri ile aynı yaklaşım).
    /// </summary>
    public class FinansmanKisitlamaOrani
    {
        public int Id { get; set; }

        /// <summary>Hesap yılı. Benzersizdir — yıl başına tek oran.</summary>
        public int Yil { get; set; }

        /// <summary>
        /// Kısıtlama oranı <b>yüzde olarak</b>: %10 için <c>10</c> yazılır (0,10 değil).
        /// Ekranda da yüzde girildiği için dönüşüm tek yerde, motorun içinde yapılıyor.
        /// </summary>
        public decimal Oran { get; set; }

        /// <summary>Yasal dayanak. Örn: "3490 sayılı Cumhurbaşkanı Kararı".</summary>
        public string? Dayanak { get; set; }

        public string? Not { get; set; }

        public DateTime? GuncellenmeTarihi { get; set; }
    }
}
