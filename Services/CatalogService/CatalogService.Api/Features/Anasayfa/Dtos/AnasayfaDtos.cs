namespace CatalogService.Api.Features.Anasayfa.Dtos
{
    /// <summary>Bir firmanın banka otomasyonunda onay bekleyen satır sayısı.</summary>
    public class AnasayfaBankaSatiriDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public int OnayBekleyen { get; set; }
    }

    /// <summary>Son ödeme tarihi yaklaşan (ya da geçmiş) beyanname.</summary>
    public class AnasayfaOdemeDto
    {
        public int DeclarationId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string BeyannameTuru { get; set; } = string.Empty;
        public DateTime SonOdemeTarihi { get; set; }
        public decimal Tutar { get; set; }

        /// <summary>Bugünden kaç gün sonra; negatifse gecikmiş.</summary>
        public int GunKaldi { get; set; }

        public bool Gecikmis => GunKaldi < 0;
    }

    /// <summary>
    /// Anasayfanın tek çağrıda okuduğu özet. Her sayı, ekranda tıklanınca gidilecek
    /// sayfanın kendi verisiyle aynı kaynaktan geliyor — anasayfa kendi hesabını yapmıyor.
    /// </summary>
    public class AnasayfaOzetDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }

        /// <summary>Bu ay ödemesi tamamlanmamış beyanname sayısı.</summary>
        public int BekleyenBeyannameSayisi { get; set; }

        /// <summary>Bu ay ödemesi tamamlanmamış beyannamelerin toplam tutarı.</summary>
        public decimal BekleyenVergiTutari { get; set; }

        public int ToplamBeyannameSayisi { get; set; }
        public decimal ToplamVergiTutari { get; set; }

        /// <summary>Yalnız onay bekleyeni olan firmalar; en çok bekleyen üstte.</summary>
        public List<AnasayfaBankaSatiriDto> BankaOnayBekleyen { get; set; } = new();

        public int BankaOnayBekleyenToplam { get; set; }

        /// <summary>Yaklaşan (ve gecikmiş) ödemeler, tarihe göre sıralı.</summary>
        public List<AnasayfaOdemeDto> YaklasanOdemeler { get; set; } = new();

        /// <summary>Yaklaşan ödemelerin kaç günlük pencerede arandığı; ekran bunu yazıyor.</summary>
        public int OdemePenceresiGun { get; set; }
    }
}
