namespace CatalogService.Api.Features.Anasayfa.Domain
{
    /// <summary>
    /// Firmaya düşülen serbest not. Firma başına birden çok olabilir; kartta en sonuncusu
    /// görünür. Silme yalnız notu yazana açık.
    /// </summary>
    public class FirmaNotu
    {
        public long Id { get; set; }

        public int FirmaId { get; set; }

        public string Metin { get; set; } = string.Empty;

        /// <summary>JWT <c>sub</c> — IdentityService kullanıcı Id'si (servisler arası FK yok).</summary>
        public string OlusturanKullaniciId { get; set; } = string.Empty;

        /// <summary>Ekranda gösterilen ad; yazıldığı andaki hâli.</summary>
        public string? OlusturanKullaniciAdi { get; set; }

        /// <summary>UTC.</summary>
        public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Firma olay kaydının türü — sabit liste. <b>Yalnız yazma işlemleri</b>; görüntüleme
    /// kaydı tutulmaz (her ekran açılışı listeyi kullanışsız yapar ve çok kullanıcılı
    /// ortamda "izleniyorum" hissi yaratır).
    /// </summary>
    public enum FirmaOlayTipi : byte
    {
        FirmaOlusturuldu = 1,
        MukellefiyetGuncellendi = 2,
        SicilGuncellendi = 3,
        SiniflandirmaGuncellendi = 4,
        SorumluAtandi = 5,
        NotEklendi = 6,
        MizanYuklendi = 7,
        EtiketKuraliEklendi = 8,
        EtiketKuraliSilindi = 9,
        IsEklendi = 10,
        IsGuncellendi = 11,
        IsSilindi = 12,
        IsYapildi = 13,
        IsIsaretiKaldirildi = 14
    }

    /// <summary>Takip kartındaki "Son işlemler" satırı.</summary>
    public class FirmaOlayKaydi
    {
        public long Id { get; set; }

        public int FirmaId { get; set; }

        public FirmaOlayTipi OlayTipi { get; set; }

        public string Aciklama { get; set; } = string.Empty;

        public string? KullaniciId { get; set; }
        public string? KullaniciAdi { get; set; }

        /// <summary>UTC.</summary>
        public DateTime Zaman { get; set; } = DateTime.UtcNow;
    }
}
