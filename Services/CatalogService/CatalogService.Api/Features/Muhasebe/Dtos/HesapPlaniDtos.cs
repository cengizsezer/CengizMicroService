using CatalogService.Api.Features.Muhasebe.Domain;

namespace CatalogService.Api.Features.Muhasebe.Dtos
{
    /// <summary>
    /// Hesap planı düğümü. Ağaç tek istekte düz liste olarak çekilir ve
    /// istemci tarafında <see cref="UstHesapId"/> üzerinden kurulur.
    /// </summary>
    public class HesapPlaniDto
    {
        public int Id { get; set; }
        public int? UstHesapId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string KodDuz { get; set; } = string.Empty;
        public string SegmentKod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public byte Seviye { get; set; }
        public HesapTuru HesapTuru { get; set; }
        public HesapKarakter Karakter { get; set; }
        public bool HareketGorur { get; set; }
        public bool SistemHesabi { get; set; }
        public string? ParaBirimi { get; set; }
        public string? BankaKodu { get; set; }
        public string? Iban { get; set; }
        public string Yol { get; set; } = string.Empty;
        public bool Aktif { get; set; }

        /// <summary>İstemcinin ağacı kurarken doldurduğu alan; sunucu boş döner.</summary>
        public List<HesapPlaniDto> Cocuklar { get; set; } = new();
    }

    /// <summary>
    /// Hesap ekleme isteği. Kullanıcıdan tam kod alınmaz (iş kuralı 3): üst hesap
    /// seçilir, yalnızca son segment girilir; tam kodu servis birleştirir.
    /// Karakter alanı istenmez, üst hesaptan miras alınır (iş kuralı 5).
    /// </summary>
    public class HesapPlaniCreateDto
    {
        public int? UstHesapId { get; set; }
        public string Segment { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public bool HareketGorur { get; set; } = true;

        /// <summary>Döviz takipli hesaplarda dolu, ör. "USD".</summary>
        public string? ParaBirimi { get; set; }

        /// <summary>TCMB EFT katılımcı kodu (banka muavinlerinde).</summary>
        public string? BankaKodu { get; set; }
        public string? Iban { get; set; }
    }

    /// <summary>
    /// Hesap güncelleme isteği. <see cref="Segment"/> boş bırakılırsa kod değişmez.
    /// Sistem hesaplarında yalnızca <see cref="Ad"/> güncellenebilir (iş kuralı 6).
    /// </summary>
    public class HesapPlaniUpdateDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Segment { get; set; }
        public bool HareketGorur { get; set; }
        public string? ParaBirimi { get; set; }
        public string? BankaKodu { get; set; }
        public string? Iban { get; set; }
    }

    /// <summary>Üst hesabın altındaki ilk boş kod. Ekleme diyaloğu bununla açılır.</summary>
    public class SonrakiKodDto
    {
        public int UstHesapId { get; set; }
        public byte Seviye { get; set; }
        public string Segment { get; set; } = string.Empty;

        /// <summary>Üst hesabın kodu ile birleştirilmiş tam kod.</summary>
        public string Kod { get; set; } = string.Empty;
    }

    /// <summary>Bir grubun altında kullanılmamış kebir kodu (iş kuralı 7).</summary>
    public class BosKebirDto
    {
        public string Segment { get; set; } = string.Empty;
        public string Kod { get; set; } = string.Empty;
    }

    /// <summary>
    /// TCMB EFT katılımcı kodu. Banka muavini açılırken hesap kodu bu listeden gelir,
    /// kullanıcı elle yazmaz.
    /// </summary>
    public class BankaKoduDto
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
    }

    /// <summary>Hesap silme sonucu (iş kuralı 6 ve 8).</summary>
    public enum HesapSilmeSonuc
    {
        Silindi = 0,
        Bulunamadi = 1,
        SistemHesabi = 2,
        AltHesapVar = 3,
        HareketVar = 4
    }
}
