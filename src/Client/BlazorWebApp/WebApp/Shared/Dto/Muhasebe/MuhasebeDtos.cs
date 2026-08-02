namespace WebApp.Shared.Dto.Muhasebe
{
    /// <summary>Hesabın ağaçtaki seviyesi/rolü. Sunucudaki <c>HesapTuru</c> ile aynı sayısal değerler.</summary>
    public enum HesapTuru : byte
    {
        Sinif = 1,
        Grup = 2,
        Kebir = 3,
        Muavin = 4
    }

    /// <summary>Hesabın bakiye karakteri. Sunucudaki <c>HesapKarakter</c> ile aynı sayısal değerler.</summary>
    public enum HesapKarakter : byte
    {
        Aktif = 1,
        Pasif = 2,
        Gelir = 3,
        Gider = 4,
        Maliyet = 5,
        Nazim = 6
    }

    /// <summary>Bakiyenin hangi tarafta kaldığı.</summary>
    public enum BakiyeYonu : byte
    {
        Yok = 0,
        Borc = 1,
        Alacak = 2
    }

    /// <summary>
    /// Hesap planı düğümü. Sunucu düz liste döner; ağaç <see cref="UstHesapId"/> üzerinden
    /// istemcide kurulur ve <see cref="Cocuklar"/> doldurulur.
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

        /// <summary>Materialized path, ör. "/1/2/". Ata hesap Id'lerini içerir.</summary>
        public string Yol { get; set; } = string.Empty;

        public bool Aktif { get; set; }

        /// <summary>İstemcide doldurulur; sunucu boş döner.</summary>
        public List<HesapPlaniDto> Cocuklar { get; set; } = new();
    }

    /// <summary>
    /// Hesap ekleme isteği. Tam kod gönderilmez (iş kuralı 3): üst hesap ve yalnızca
    /// son segment gönderilir, tam kodu sunucu birleştirir. Karakter gönderilmez,
    /// üst hesaptan miras alınır (iş kuralı 5).
    /// </summary>
    public class HesapPlaniCreateDto
    {
        public int? UstHesapId { get; set; }
        public string Segment { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public bool HareketGorur { get; set; } = true;
        public string? ParaBirimi { get; set; }
        public string? BankaKodu { get; set; }
        public string? Iban { get; set; }
    }

    /// <summary>Hesap güncelleme isteği. <see cref="Segment"/> boşsa kod değişmez.</summary>
    public class HesapPlaniUpdateDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Segment { get; set; }
        public bool HareketGorur { get; set; }
        public string? ParaBirimi { get; set; }
        public string? BankaKodu { get; set; }
        public string? Iban { get; set; }
    }

    /// <summary>
    /// Üst hesabın altındaki ilk boş kod. Ekleme diyaloğu bununla açılır; ayrıca
    /// <see cref="Kod"/> ile <see cref="Segment"/> farkından kod öneki ve segment
    /// uzunluğu çıkarılır, böylece istemci kod maskesini bilmek zorunda kalmaz.
    /// </summary>
    public class SonrakiKodDto
    {
        public int UstHesapId { get; set; }
        public byte Seviye { get; set; }
        public string Segment { get; set; } = string.Empty;
        public string Kod { get; set; } = string.Empty;
    }

    /// <summary>Bir grubun altında kullanılmamış kebir kodu (iş kuralı 7).</summary>
    public class BosKebirDto
    {
        public string Segment { get; set; } = string.Empty;
        public string Kod { get; set; } = string.Empty;
    }

    /// <summary>
    /// TCMB EFT katılımcı kodu. Liste API'nin <c>banka-kodlari</c> ucundan gelir;
    /// istemcide kopya tutulmaz.
    /// </summary>
    public class BankaKoduDto
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;

        /// <summary>Açılır listede gösterilen etiket.</summary>
        public string Etiket => $"{Kod} — {Ad}";
    }

    // ---- Mizan (hem ağaçtaki bakiye kolonu hem mizan ekranı için) ----

    public class MizanSatirDto
    {
        public int HesapId { get; set; }
        public int? UstHesapId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public byte Seviye { get; set; }
        public HesapTuru HesapTuru { get; set; }
        public HesapKarakter Karakter { get; set; }
        public bool HareketGorur { get; set; }
        public bool Aktif { get; set; }

        /// <summary>
        /// Alt hesabı olmayan düğüm. Üst satırlar alt ağacın toplamını taşıdığı için
        /// (iş kuralı 19) mükerrersiz tutar yalnızca yaprak satırlardadır; mizan ekranı
        /// üst satırları bu yüzden soluk gösterir.
        /// </summary>
        public bool YaprakMi { get; set; }

        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }
        public decimal Bakiye { get; set; }
        public BakiyeYonu Yon { get; set; }
    }

    public class MizanToplamDto
    {
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }
        public bool Dengede { get; set; }
    }

    /// <summary>İş kuralı 21: taslak fişler mizana/T cetveline girmez, ayrı gösterilir.</summary>
    public class TaslakOzetDto
    {
        public int FisSayisi { get; set; }
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
    }

    public class MizanDto
    {
        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }
        public byte? Seviye { get; set; }
        public List<MizanSatirDto> Satirlar { get; set; } = new();
        public MizanToplamDto GenelToplam { get; set; } = new();
        public TaslakOzetDto Taslak { get; set; } = new();
    }
}
