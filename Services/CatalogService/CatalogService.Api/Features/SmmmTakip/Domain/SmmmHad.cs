namespace CatalogService.Api.Features.SmmmTakip.Domain
{
    /// <summary>
    /// Bir konuya bağlı had/limit tanımı. Yıla göre değeri <see cref="SmmmHadDegeri"/>'nde tutulur.
    /// </summary>
    public class SmmmHad
    {
        public int Id { get; set; }
        public int KonuId { get; set; }

        /// <summary>Benzersiz kod. Örn: "BU_KIRA_BUYUKSEHIR".</summary>
        public string Kod { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;

        /// <summary>"TL" | "%" | "adet".</summary>
        public string Birim { get; set; } = "TL";

        /// <summary>Yasal dayanak. Örn: "GVK 47/2".</summary>
        public string? Dayanak { get; set; }

        /// <summary>Konu içindeki gösterim sırası.</summary>
        public int Sira { get; set; }

        public ICollection<SmmmHadDegeri> Degerler { get; set; } = new List<SmmmHadDegeri>();
    }
}
