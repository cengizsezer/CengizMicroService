namespace CatalogService.Api.Features.MevzuatGrafigi.Domain
{
    /// <summary>
    /// Mevzuat grafiğindeki kartın türü. Bağlantı sırası bu türlere göre kurulur
    /// (bkz. <see cref="BaglantiKurali"/>). Değerler veritabanında byte saklanır; sırayı değiştirmeyin.
    /// </summary>
    public enum MevzuatKartTuru : byte
    {
        Firma = 1,
        Islem = 2,
        Istisna = 3,
        Indirim = 4,
        Kanun = 5,
        Sart = 6
    }

    /// <summary>
    /// Mevzuat grafiğinin bir kartı. Kartlar tekildir: bir kart birden çok tuvalde görünebilir,
    /// düzenleme her tuvale yansır. Ortak referans içeriktir; tenant'a bağlı DEĞİLDİR.
    /// Firma türündeki kartlar gerçek Firma kaydına bağlı değildir, serbest metin profildir.
    /// </summary>
    public class MevzuatKart
    {
        public int Id { get; set; }

        public MevzuatKartTuru Tur { get; set; }

        public string Baslik { get; set; } = string.Empty;

        /// <summary>Örn: "KDVK 11/1-a". Firma kartında profil ("İhracatçı · İthalatçı").</summary>
        public string? Madde { get; set; }

        public string? Ozet { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
        public DateTime? GuncellemeTarihi { get; set; }
    }
}
