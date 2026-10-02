using WebApp.Shared.Dto.FirmaBilgileri;

namespace WebApp.Shared.Dto.Anasayfa
{
    /// <summary>Uyarı türü; sayısal değerler sunucuyla ortak. Ekran türe göre simge seçer.</summary>
    public enum FirmaUyariTuru : byte
    {
        ImzaYetkisiBitiyor = 1,
        PayOraniTutmuyor = 2,
        EksikSicilAlani = 3,
        GecerliYetkiliYok = 4,
        SiniflandirmaUyusmuyor = 5
    }

    public class FirmaUyariDto
    {
        public FirmaUyariTuru Tur { get; set; }

        /// <summary>Sunucunun cümlesi olduğu gibi gösterilir; istemci kendi metnini üretmez.</summary>
        public string Mesaj { get; set; } = string.Empty;
    }

    /// <summary>Raydaki rozetin türü (sunucu: FirmaEksikBilgi.Rozet). Renk ve metin bundan; istemci eşik tanımlamaz.</summary>
    public enum KunyeRozeti : byte
    {
        Yok = 0,
        GerekliEksik = 1,
        Uyari = 2,
        Tamamlaniyor = 3
    }

    /// <summary>Sol listedeki firma satırı.</summary>
    public class FirmaPaneliOzetDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string VergiKimlikNo { get; set; } = string.Empty;
        public List<FirmaUyariDto> Uyarilar { get; set; } = new();

        public bool UyariVar => Uyarilar.Count > 0;

        /// <summary>Boş künye maddesi (gerekli + ikincil + durum); rozet bunu SAYMAZ.</summary>
        public int EksikSayisi { get; set; }

        /// <summary>Boş GEREKLİ alan sayısı — rozetin sayısı.</summary>
        public int GerekliEksikSayisi { get; set; }

        /// <summary>Rozetin title'ı: "Eksik: vergi dairesi, defter usulü".</summary>
        public List<string> GerekliEksikler { get; set; } = new();

        public bool YeniFirma { get; set; }
        public KunyeRozeti Rozet { get; set; }

        /// <summary>İmza yetkisi / pay oranı / geçerli yetkili yok uyarılarının sayısı.</summary>
        public int UyariSayisi { get; set; }

        /// <summary>Firmanın HİÇ mizanı yok (kırmızı "!").</summary>
        public bool MizanYok { get; set; }

        public string? MizanFormati { get; set; }
    }

    /// <summary>Eksik künye alanı. <see cref="Odak"/> "Tamamla"nın gideceği alan anahtarı.</summary>
    public class EksikAlanDto
    {
        public string Kart { get; set; } = string.Empty;
        public string Alan { get; set; } = string.Empty;
        public string Odak { get; set; } = string.Empty;

        /// <summary>Gerekli alan (rozete girer); değilse ikincil ya da durum.</summary>
        public bool Gerekli { get; set; }

        /// <summary>Künye eksiği değil durum (cari dönem mizanı); rozete girmez.</summary>
        public bool Durum { get; set; }
    }

    public class FirmaMukellefiyetDto
    {
        public string VergiKimlikNo { get; set; } = string.Empty;
        public string? VergiDairesi { get; set; }
        public string? MukellefiyetTurleri { get; set; }

        /// <summary>Sunucunun ayıkladığı kodlar; kartta çip.</summary>
        public List<MukellefiyetKoduDto> Kodlar { get; set; } = new();

        /// <summary>Kod sayılamayan kalan metin; çiplerin altında aynen gösterilir.</summary>
        public string? KalanMetin { get; set; }

        public bool? EFatura { get; set; }
        public bool? EDefter { get; set; }
        public DateTime? IseBaslamaTarihi { get; set; }
        public string? NaceKodu { get; set; }
    }

    public class MukellefiyetKoduDto
    {
        public string Kod { get; set; } = string.Empty;
        public string? Ad { get; set; }
        public string? KisaAd { get; set; }
        public bool SiniflandirmaKaynagi { get; set; }
    }

    public class FirmaPaneliSicilDto
    {
        public string? TicaretSicilNo { get; set; }
        public string? MersisNo { get; set; }
        public decimal? Sermaye { get; set; }
        public string? SermayeParaBirimi { get; set; }
        public DateTime? KurulusTarihi { get; set; }
        public string? Adres { get; set; }
    }

    // ---- Sınıflandırma ve mizan (sayısal değerler sunucuyla ortak) ----

    public enum DefterUsulu : byte
    {
        Belirsiz = 0,
        BilancoEsasi = 1,
        IsletmeHesabi = 2
    }

    public enum VergiTuru : byte
    {
        Belirsiz = 0,
        KurumlarVergisi = 1,
        GelirVergisi = 2
    }

    public enum HesapDonemi : byte
    {
        TakvimYili = 1,
        OzelHesapDonemi = 2
    }

    public enum FirmaTipi : byte
    {
        Belirsiz = 0,
        SermayeSirketi = 1,
        Sahis = 2,
        AdiOrtaklik = 3,
        IsOrtakligi = 4
    }

    public enum SgkTesvikKademesi : byte
    {
        Belirsiz = 0,
        Tesviksiz = 1,
        ImalatDisi = 2,
        Imalat = 3
    }

    public enum MuhtasarDonemi : byte
    {
        Belirsiz = 0,
        Aylik = 1,
        UcAylik = 2
    }

    /// <summary>SGK kademesi ve işveren oranı — oranlar SUNUCUDAN gelir (SgkTesvikOranlari), burada kopya yok.</summary>
    public class SgkKademeSecenegiDto
    {
        public SgkTesvikKademesi Kademe { get; set; }
        public string Ad { get; set; } = string.Empty;
        public decimal IsverenOrani { get; set; }
    }

    public class FirmaSistemAtamasiDto
    {
        public int SistemId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public WebApp.Shared.Dto.Sistemler.SistemTuru Tur { get; set; }
        public bool Aktif { get; set; }
        public string? FirmaKodu { get; set; }
        public int Sira { get; set; }
    }

    public class FirmaPaneliSistemlerDto
    {
        public List<FirmaSistemAtamasiDto> Atamalar { get; set; } = new();
        public string? SistemNotu { get; set; }
    }

    /// <summary>
    /// Mizan formatı seçenekleri; sunucudaki <c>MizanFormatlari.Liste</c> ile aynı. Profil
    /// tablosu gelene kadar sabit liste.
    /// </summary>
    public static class MizanFormatlari
    {
        public const string Belirsiz = "Belirsiz";

        public static readonly IReadOnlyList<string> Liste = new[]
        {
            "ORKA — döviz kolonlu",
            "ORKA — döviz kolonsuz",
            "Luca",
            "Mikro",
            "Logo",
            Belirsiz
        };

        public static bool Secili(string? format)
            => !string.IsNullOrWhiteSpace(format) && format != Belirsiz;

        /// <summary>
        /// Boş şablonu olan formatlar: yalnız gerçek çıktısı görülmüş programlar (sunucudaki
        /// liste ile aynı). Mikro/Logo için şablon yok — uydurma şablon gönderilmez.
        /// </summary>
        public static bool SablonVar(string? format)
            => format is "ORKA — döviz kolonlu" or "ORKA — döviz kolonsuz" or "Luca";

        /// <summary>
        /// Muhasebe programına uyan formatlar — yalnız öneri (sunucudaki MizanFormatlari.ProgramIcin
        /// ile aynı kural: formatın "—" öncesi program adı, büyük/küçük harf ve boşluk farkı yok).
        /// </summary>
        public static List<string> ProgramIcin(string? programAdi)
        {
            static string N(string? s) => new string((s ?? string.Empty).ToLower(new System.Globalization.CultureInfo("tr-TR"))
                .Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '-' or '_')).Select(c => c == 'ı' ? 'i' : c).ToArray());

            var ad = N(programAdi);
            if (ad.Length == 0) return new();
            return Liste.Where(f => f != Belirsiz && N(f.Split('—')[0]) == ad).ToList();
        }
    }

    /// <summary>Son yüklenen mizanın durumu; özet alanları eski kayıtlarda boş ("—").</summary>
    public class FirmaPaneliMizanDurumuDto
    {
        public int Yil { get; set; }
        public int Donem { get; set; }
        public int AnalizYili { get; set; }
        public DateTime? YuklenmeZamani { get; set; }
        public int? SatirSayisi { get; set; }
        public int? SeviyeSayisi { get; set; }
        public decimal? BorcToplam { get; set; }
        public decimal? AlacakToplam { get; set; }
        public decimal? Fark { get; set; }
        public bool? Dengeli { get; set; }
    }

    public class FirmaPaneliSiniflandirmaDto
    {
        public DefterUsulu DefterUsulu { get; set; }
        public VergiTuru VergiTuru { get; set; }
        public string? DefterUsuluOtomatikKod { get; set; }
        public string? VergiTuruOtomatikKod { get; set; }
        public string? MizanFormati { get; set; }
        public string? OrkaFirmaKodu { get; set; }
        public HesapDonemi? HesapDonemi { get; set; }
        public DateTime? OzelDonemBas { get; set; }
        public DateTime? OzelDonemBit { get; set; }
        public FirmaPaneliMizanDurumuDto? SonMizan { get; set; }
        public string? UyusmazlikMesaji { get; set; }

        public FirmaTipi FirmaTipi { get; set; }
        public SgkTesvikKademesi SgkTesvikKademesi { get; set; }
        public MuhtasarDonemi MuhtasarDonemi { get; set; }
        public decimal? SgkIsverenOrani { get; set; }
        public List<SgkKademeSecenegiDto> SgkKademeleri { get; set; } = new();
    }

    // ---- Takip kartı (sayısal değerler sunucuyla ortak) ----

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

    public class FirmaDonemCipDto
    {
        public int Yil { get; set; }
        public bool Yuklu { get; set; }
    }

    public class FirmaNotuDto
    {
        public long Id { get; set; }
        public string Metin { get; set; } = string.Empty;
        public string? OlusturanKullaniciAdi { get; set; }
        public DateTime OlusturmaZamani { get; set; }
        public bool Silinebilir { get; set; }
    }

    public class FirmaOlayDto
    {
        public FirmaOlayTipi OlayTipi { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public string? KullaniciAdi { get; set; }
        public DateTime Zaman { get; set; }
    }

    public class FirmaPaneliTakipDto
    {
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
        public List<FirmaDonemCipDto> Donemler { get; set; } = new();
        public FirmaNotuDto? SonNot { get; set; }
        public int NotSayisi { get; set; }
        public List<FirmaOlayDto> SonOlaylar { get; set; } = new();
    }

    /// <summary>İmza yetkilileri kartı başlık rozeti; sunucu hesaplar.</summary>
    public class FirmaYetkiRozetiDto
    {
        public int Sayi { get; set; }
        public bool Kritik { get; set; }
        public bool GecerliYok { get; set; }
    }

    public class FirmaSorumluAtaDto
    {
        public int? KullaniciId { get; set; }
        public string? KullaniciAdi { get; set; }
    }

    public class FirmaNotuEkleDto
    {
        public string Metin { get; set; } = string.Empty;
    }

    public class FirmaSiniflandirmaKaydetDto
    {
        public DefterUsulu DefterUsulu { get; set; }
        public VergiTuru VergiTuru { get; set; }

        /// <summary>Gönderilmez (null = dokunma): mizan formatı Kullanılan sistemler formunda.</summary>
        public string? MizanFormati { get; set; }

        public HesapDonemi? HesapDonemi { get; set; }
        public DateTime? OzelDonemBas { get; set; }
        public DateTime? OzelDonemBit { get; set; }
        public FirmaTipi FirmaTipi { get; set; }
        public SgkTesvikKademesi SgkTesvikKademesi { get; set; }
        public MuhtasarDonemi MuhtasarDonemi { get; set; }

        /// <summary>Kayıtlı sınıflandırmayı kodlarla uyuşmasa da onayla (kaynak "elle" olur).</summary>
        public bool Onayla { get; set; }
    }

    public class FirmaPaneliYetkiliDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Tckn { get; set; }
        public string? Gorev { get; set; }
        public TemsilSekli TemsilSekli { get; set; }
        public DateTime? YetkiBitis { get; set; }

        /// <summary>Bitişe kalan gün; negatifse dolmuş, <c>null</c> ise süresiz.</summary>
        public int? KalanGun { get; set; }

        public bool SuresiDoldu { get; set; }
    }

    public class FirmaPaneliDetayDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;

        public int GerekliEksikSayisi { get; set; }
        public bool YeniFirma { get; set; }

        public FirmaMukellefiyetDto Mukellefiyet { get; set; } = new();
        public FirmaPaneliSiniflandirmaDto Siniflandirma { get; set; } = new();
        public FirmaPaneliSistemlerDto Sistemler { get; set; } = new();
        public FirmaPaneliSicilDto Sicil { get; set; } = new();
        public FirmaOrtaklikDto Ortaklik { get; set; } = new();
        public List<FirmaPaneliYetkiliDto> Yetkililer { get; set; } = new();
        public FirmaYetkiRozetiDto? YetkiRozeti { get; set; }
        public FirmaPaneliTakipDto Takip { get; set; } = new();
        public List<FirmaBelgesiDto> Belgeler { get; set; } = new();
        public List<FirmaUyariDto> Uyarilar { get; set; } = new();
        public List<EksikAlanDto> Eksikler { get; set; } = new();
    }

    /// <summary>
    /// Firma panelinin tek çağrılık yanıtı: tüm firmaların satırları + seçili firmanın
    /// ayrıntısı. Ekran açılışta firma başına ayrı istek atmıyor.
    /// </summary>
    public class FirmaPaneliDto
    {
        public List<FirmaPaneliOzetDto> Firmalar { get; set; } = new();
        public FirmaPaneliDetayDto? Secili { get; set; }

        /// <summary>Sunucunun yeni firma toleransı (gün); istemcide sabit yok.</summary>
        public int YeniFirmaToleransGun { get; set; }
    }
}
