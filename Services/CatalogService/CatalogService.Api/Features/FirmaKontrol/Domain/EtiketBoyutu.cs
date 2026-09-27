using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Etiket boyutu — etiketlerin hangi eksende verildiği ("Grup Şirketi", "Hizmet Hattı").
    ///
    /// DÖNEMDEN BAĞIMSIZ: kural hesap koduna bağlıdır, her dönemde aynı kurallar çalışır;
    /// bu yüzden <c>Yil</c> ya da <c>Donem</c> alanı YOKTUR.
    ///
    /// Kurallar TANIMDIR, türetilmiş veri değildir: hangi düğüme hangi etiketin düştüğü
    /// SAKLANMAZ, çalışma zamanında hesaplanır.
    /// </summary>
    public class EtiketBoyutu
    {
        public int Id { get; set; }

        public string Ad { get; set; } = string.Empty;

        public EtiketKapsami Kapsam { get; set; }

        /// <summary>Kapsam = BuFirma ise dolu; TumFirmalar ise null.</summary>
        public int? FirmaId { get; set; }
        public Firma? Firma { get; set; }

        public int Sira { get; set; }

        /// <summary>
        /// Boyutun kapsadığı ana hesaplar: virgülle ayrılmış ana hesap kodu deseni listesi,
        /// sondaki * joker. Örnek: "600,601,610" ya da "6*".
        ///
        /// Kapsam yüzdesi, Atanmamış tutarı ve "hiçbir kurala düşmeyen hesap" uyarısı
        /// YALNIZCA bu kapsamdaki hesaplar üzerinden hesaplanır. Kapsam olmadan payda
        /// bütün gelir tablosunun NETİ olur (gelirler artı, giderler eksi birbirini yer)
        /// ve yüzde %100'ü aşar.
        /// </summary>
        public string KapsamHesaplari { get; set; } = string.Empty;

        public ICollection<EtiketDegeri> Degerler { get; set; } = new List<EtiketDegeri>();
        public ICollection<EtiketKurali> Kurallar { get; set; } = new List<EtiketKurali>();
    }
}
