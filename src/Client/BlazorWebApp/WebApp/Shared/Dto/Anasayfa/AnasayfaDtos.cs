namespace WebApp.Shared.Dto.Anasayfa
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

    public class AnasayfaOzetDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }

        public int BekleyenBeyannameSayisi { get; set; }
        public decimal BekleyenVergiTutari { get; set; }
        public int ToplamBeyannameSayisi { get; set; }
        public decimal ToplamVergiTutari { get; set; }

        public List<AnasayfaBankaSatiriDto> BankaOnayBekleyen { get; set; } = new();
        public int BankaOnayBekleyenToplam { get; set; }

        public List<AnasayfaOdemeDto> YaklasanOdemeler { get; set; } = new();
        public int OdemePenceresiGun { get; set; }
    }

    /// <summary>
    /// Hızlı erişimde gösterilen "son kullanılan firma". Tarayıcıda (localStorage)
    /// tutulur — kullanıcının kendi gezinme geçmişi, sunucuya yazılacak bir veri değil.
    /// </summary>
    public class SonFirmaDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public DateTime SonKullanim { get; set; }
    }
}
