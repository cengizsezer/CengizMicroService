using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;

namespace CatalogService.Api.Features.Anasayfa.Dtos
{
    /// <summary>
    /// Firma satırında gösterilen uyarı türü. Kullanıcı firmaya tıklamadan, listedeki
    /// simgeden sorunun ne olduğunu görebilsin diye tür ayrı taşınıyor — ekran türe göre
    /// simge/renk seçiyor, metni sunucudan olduğu gibi yazıyor.
    /// </summary>
    public enum FirmaUyariTuru : byte
    {
        ImzaYetkisiBitiyor = 1,
        PayOraniTutmuyor = 2,
        EksikSicilAlani = 3,

        /// <summary>Kayıtlı imza yetkililerinin hepsinin süresi dolmuş.</summary>
        GecerliYetkiliYok = 4,

        /// <summary>Otomatik dolmuş sınıflandırma mükellefiyet kodlarıyla çelişiyor.</summary>
        SiniflandirmaUyusmuyor = 5
    }

    public class FirmaUyariDto
    {
        public FirmaUyariTuru Tur { get; set; }

        /// <summary>Kullanıcıya gösterilen cümle; istemci kendi metnini üretmiyor.</summary>
        public string Mesaj { get; set; } = string.Empty;
    }

    /// <summary>Sol listedeki bir firma satırı.</summary>
    public class FirmaPaneliOzetDto
    {
        public int FirmaId { get; set; }

        /// <summary>Kısa ad varsa o, yoksa unvan — listede okunabilir olan.</summary>
        public string Ad { get; set; } = string.Empty;

        public string Unvan { get; set; } = string.Empty;

        public string VergiKimlikNo { get; set; } = string.Empty;

        public List<FirmaUyariDto> Uyarilar { get; set; } = new();

        public bool UyariVar => Uyarilar.Count > 0;

        /// <summary>Boş künye maddesi sayısı — gerekli + ikincil + durum (<c>FirmaEksikBilgi</c>). Rozet bunu SAYMAZ.</summary>
        public int EksikSayisi { get; set; }

        /// <summary>Boş GEREKLİ alan sayısı — rozetin sayısı (Prompt 12).</summary>
        public int GerekliEksikSayisi { get; set; }

        /// <summary>Boş gerekli alanların adları — rozetin <c>title</c>'ı: "Eksik: vergi dairesi, defter usulü".</summary>
        public List<string> GerekliEksikler { get; set; } = new();

        /// <summary>Firma oluşturulalı yeni firma toleransından az gün geçti.</summary>
        public bool YeniFirma { get; set; }

        /// <summary>Rozetin türü (renk + metin); karar sunucuda tek yerde (<c>FirmaEksikBilgi.Rozet</c>).</summary>
        public Services.KunyeRozeti Rozet { get; set; }

        /// <summary>
        /// Uyarı sayısı (imza yetkisi, pay oranı, geçerli yetkili yok); rozette eksik
        /// sayısıyla toplanır, şeritte ayrı cümlede yazılır.
        /// </summary>
        public int UyariSayisi { get; set; }

        /// <summary>Firmanın HİÇ mizanı yok (kırmızı "!").</summary>
        public bool MizanYok { get; set; }

        /// <summary>Satırın ikinci satırında VKN'in yanında gösterilir; seçilmemişse <c>null</c>.</summary>
        public string? MizanFormati { get; set; }
    }

    /// <summary>Eksik bir künye alanı: hangi kartta, ne eksik, "Tamamla" nereye odaklanır.</summary>
    public class EksikAlanDto
    {
        public string Kart { get; set; } = string.Empty;
        public string Alan { get; set; } = string.Empty;

        /// <summary>Alanın anahtarı ("mersisNo", "sorumlu"…); istemci kartı/alanı bununla bulur.</summary>
        public string Odak { get; set; } = string.Empty;

        /// <summary>Gerekli alan (rozete girer). Değilse ikincil ya da durum.</summary>
        public bool Gerekli { get; set; }

        /// <summary>Künye eksiği değil durum ("cari dönem mizanı yüklenmemiş"); rozete girmez.</summary>
        public bool Durum { get; set; }
    }

    /// <summary>Mükellefiyet bölümü. Alanların bir kısmı <c>catalog.Firmalar</c>'dan.</summary>
    public class FirmaMukellefiyetDto
    {
        public string VergiKimlikNo { get; set; } = string.Empty;
        public string? VergiDairesi { get; set; }
        public string? MukellefiyetTurleri { get; set; }

        /// <summary>Metinden ayıklanan kodlar — kartta çip olarak çizilir.</summary>
        public List<MukellefiyetKoduDto> Kodlar { get; set; } = new();

        /// <summary>Kod sayılamayan kalan metin; varsa çiplerin altında aynen gösterilir.</summary>
        public string? KalanMetin { get; set; }

        public bool? EFatura { get; set; }
        public bool? EDefter { get; set; }
        public DateTime? IseBaslamaTarihi { get; set; }
        public string? NaceKodu { get; set; }
    }

    /// <summary>Mükellefiyet kodu çipi: "0003 · Muhtasar".</summary>
    public class MukellefiyetKoduDto
    {
        public string Kod { get; set; } = string.Empty;

        /// <summary>Metinde kodun yanında yazan tam ad.</summary>
        public string? Ad { get; set; }

        public string? KisaAd { get; set; }

        /// <summary>Vergi türü / defter usulü önerisinin kaynağı (0010 ya da 0001) — vurgulu çizilir.</summary>
        public bool SiniflandirmaKaynagi { get; set; }
    }

    /// <summary>Sicil bölümü. Okuma odaklı; düzenleme Firma Bilgileri ekranında.</summary>
    public class FirmaPaneliSicilDto
    {
        public string? TicaretSicilNo { get; set; }
        public string? MersisNo { get; set; }
        public decimal? Sermaye { get; set; }
        public string? SermayeParaBirimi { get; set; }
        public DateTime? KurulusTarihi { get; set; }
        public string? Adres { get; set; }
    }

    /// <summary>
    /// Bir mizan yüklemesinin anasayfadaki durumu. Değerler yükleme sırasında yazılmış
    /// özetten okunuyor; burada hesap yapılmıyor. Özet alanları gelmeden önce yüklenmiş
    /// kayıtlarda boş (<c>null</c>) — ekran "—" yazar.
    /// </summary>
    public class FirmaPaneliMizanDurumuDto
    {
        /// <summary>Mizanın ait olduğu yıl (önceki dönem yüklemesinde analiz yılının bir eksiği).</summary>
        public int Yil { get; set; }

        /// <summary>0 = önceki dönem, 1 = cari dönem (Firma Kontrol'deki anahtar).</summary>
        public int Donem { get; set; }

        /// <summary>Firma Kontrol'de hangi analiz yılı altında yüklendiği ("Firma kontrolünde aç" için).</summary>
        public int AnalizYili { get; set; }

        /// <summary>UTC.</summary>
        public DateTime? YuklenmeZamani { get; set; }

        public int? SatirSayisi { get; set; }
        public int? SeviyeSayisi { get; set; }
        public decimal? BorcToplam { get; set; }
        public decimal? AlacakToplam { get; set; }

        /// <summary>Borç − alacak; özet yoksa <c>null</c>.</summary>
        public decimal? Fark => BorcToplam is { } b && AlacakToplam is { } a ? b - a : null;

        /// <summary>|fark| &lt; 0,01. Özet yoksa <c>null</c>.</summary>
        public bool? Dengeli => Fark is { } f ? Math.Abs(f) < 0.01m : null;
    }

    /// <summary>
    /// "Sınıflandırma ve mizan" kartı. ORKA firma kodu Sicil kartından buraya taşındı:
    /// ticaret sicilinden gelen bir bilgi değil, bir program ayarı. Alan aynı alan.
    /// </summary>
    public class FirmaPaneliSiniflandirmaDto
    {
        public DefterUsulu DefterUsulu { get; set; }
        public VergiTuru VergiTuru { get; set; }

        /// <summary>Değer mükellefiyet kodundan otomatik geldiyse o kod ("0010"); elle ya da boşsa <c>null</c>.</summary>
        public string? DefterUsuluOtomatikKod { get; set; }
        public string? VergiTuruOtomatikKod { get; set; }

        public string? MizanFormati { get; set; }
        public string? OrkaFirmaKodu { get; set; }

        public HesapDonemi? HesapDonemi { get; set; }
        public DateTime? OzelDonemBas { get; set; }
        public DateTime? OzelDonemBit { get; set; }

        /// <summary>En son yüklenen mizan; hiç yükleme yoksa <c>null</c>.</summary>
        public FirmaPaneliMizanDurumuDto? SonMizan { get; set; }

        /// <summary>
        /// Kayıtlı sınıflandırma mükellefiyet kodlarıyla çelişiyorsa açıklaması (uyarı ile aynı
        /// cümle); düzenleme formu "kayıtlı değeri onayla" seçeneğini buna göre gösterir.
        /// </summary>
        public string? UyusmazlikMesaji { get; set; }

        // ---- Prompt 9 ----

        public FirmaTipi FirmaTipi { get; set; }
        public SgkTesvikKademesi SgkTesvikKademesi { get; set; }
        public MuhtasarDonemi MuhtasarDonemi { get; set; }

        /// <summary>Kademenin işveren SGK oranı (%); kademe seçilmemişse boş.</summary>
        public decimal? SgkIsverenOrani { get; set; }

        /// <summary>Formun seçenekleri; oranlar tek yerden (<c>SgkTesvikOranlari</c>), istemcide kopya yok.</summary>
        public List<SgkKademeSecenegiDto> SgkKademeleri { get; set; } = new();
    }

    public class SgkKademeSecenegiDto
    {
        public SgkTesvikKademesi Kademe { get; set; }
        public string Ad { get; set; } = string.Empty;
        public decimal IsverenOrani { get; set; }
    }

    /// <summary>Firmanın bir sistem ataması.</summary>
    public class FirmaSistemAtamasiDto
    {
        public int SistemId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public Sistemler.Domain.SistemTuru Tur { get; set; }

        /// <summary>Pasife alınmış sistemin mevcut ataması bozulmaz; ekranda ayrıca işaretlenir.</summary>
        public bool Aktif { get; set; }

        public string? FirmaKodu { get; set; }
        public int Sira { get; set; }
    }

    /// <summary>
    /// "Kullanılan sistemler" kartı. ORKA firma kodu, mizan formatı ve son mizan
    /// <see cref="FirmaPaneliSiniflandirmaDto"/>'da kaldı (aynı alanlar, kart yalnız gösterir).
    /// </summary>
    public class FirmaPaneliSistemlerDto
    {
        public List<FirmaSistemAtamasiDto> Atamalar { get; set; } = new();
        public string? SistemNotu { get; set; }
    }

    // ---- Takip kartı ----

    /// <summary>Bir yılın mizan durumu: yüklüyse yeşil çip, cari yıl yüklü değilse gri çip.</summary>
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

        /// <summary>UTC.</summary>
        public DateTime OlusturmaZamani { get; set; }

        /// <summary>İsteği yapan kullanıcı notu yazan kişi mi (silme yalnız ona açık).</summary>
        public bool Silinebilir { get; set; }
    }

    public class FirmaOlayDto
    {
        public Domain.FirmaOlayTipi OlayTipi { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public string? KullaniciAdi { get; set; }

        /// <summary>UTC.</summary>
        public DateTime Zaman { get; set; }
    }

    public class FirmaPaneliTakipDto
    {
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }

        /// <summary>Mizanı yüklü yıllar (yeni → eski); cari yıl yüklü değilse başta gri çip.</summary>
        public List<FirmaDonemCipDto> Donemler { get; set; } = new();

        /// <summary>En son eklenen not; yoksa <c>null</c>.</summary>
        public FirmaNotuDto? SonNot { get; set; }
        public int NotSayisi { get; set; }

        /// <summary>Son 5 olay, yeni → eski.</summary>
        public List<FirmaOlayDto> SonOlaylar { get; set; } = new();
    }

    /// <summary>
    /// İmza yetkilileri kartının başlık rozeti: bitişine 365 günden az kalan (henüz
    /// dolmamış) yetkili sayısı; biri 90 günden azsa kritik. Sayı 0 ise rozet çıkmaz.
    /// </summary>
    public class FirmaYetkiRozetiDto
    {
        public int Sayi { get; set; }
        public bool Kritik { get; set; }

        /// <summary>Kayıtlı yetkililerin hepsinin süresi dolmuş: rozet "Geçerli yetkili yok".</summary>
        public bool GecerliYok { get; set; }
    }

    public class FirmaSorumluAtaDto
    {
        /// <summary><c>null</c> = sorumluyu kaldır.</summary>
        public int? KullaniciId { get; set; }
        public string? KullaniciAdi { get; set; }
    }

    public class FirmaNotuEkleDto
    {
        public string Metin { get; set; } = string.Empty;
    }

    /// <summary>Sınıflandırma kartının "Düzenle" formu. ORKA kodu burada yazılmaz (Firma Bilgileri'nde).</summary>
    public class FirmaSiniflandirmaKaydetDto
    {
        public DefterUsulu DefterUsulu { get; set; }
        public VergiTuru VergiTuru { get; set; }

        /// <summary>
        /// <c>null</c> = dokunma. Prompt 9'dan beri mizan formatı "Kullanılan sistemler" kartında
        /// düzenleniyor (boşaltma da orada); bu alan eski istemciler için kaldı.
        /// </summary>
        public string? MizanFormati { get; set; }

        public HesapDonemi? HesapDonemi { get; set; }
        public DateTime? OzelDonemBas { get; set; }
        public DateTime? OzelDonemBit { get; set; }

        public FirmaTipi FirmaTipi { get; set; }
        public SgkTesvikKademesi SgkTesvikKademesi { get; set; }
        public MuhtasarDonemi MuhtasarDonemi { get; set; }

        /// <summary>
        /// Kayıtlı sınıflandırmayı mükellefiyet kodlarıyla uyuşmasa da onayla: dolu vergi türü ve
        /// defter usulü "elle" işaretlenir, uyuşmazlık uyarısı düşer.
        /// </summary>
        public bool Onayla { get; set; }
    }

    /// <summary>
    /// İmza yetkilisi satırı — panelin kendi görünümü.
    ///
    /// Düzenleme ekranının <see cref="FirmaImzaYetkilisiDto"/>'su kullanılmadı: orası bir
    /// <b>istek</b> gövdesi (PUT ile geri gönderiliyor), burada gereken ise ekranda
    /// yazılacak <see cref="KalanGun"/> gibi türetilmiş alanlar.
    /// </summary>
    public class FirmaPaneliYetkiliDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Tckn { get; set; }
        public string? Gorev { get; set; }
        public TemsilSekli TemsilSekli { get; set; }
        public DateTime? YetkiBitis { get; set; }

        /// <summary>Bitişe kaç gün kaldı; negatifse dolmuş. Bitiş boşsa <c>null</c> (süresiz).</summary>
        public int? KalanGun { get; set; }

        public bool SuresiDoldu { get; set; }
    }

    /// <summary>Sağ paneldeki seçili firmanın tamamı.</summary>
    public class FirmaPaneliDetayDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;

        /// <summary>Şerit başlığı: gerekli eksik sayısı ve yeni firma toleransı (raydaki rozetle aynı hesap).</summary>
        public int GerekliEksikSayisi { get; set; }
        public bool YeniFirma { get; set; }

        public FirmaMukellefiyetDto Mukellefiyet { get; set; } = new();
        public FirmaPaneliSiniflandirmaDto Siniflandirma { get; set; } = new();
        public FirmaPaneliSistemlerDto Sistemler { get; set; } = new();
        public FirmaPaneliSicilDto Sicil { get; set; } = new();

        /// <summary>
        /// Ortaklık tablosu ve toplamları. Düzenleme ekranıyla <b>aynı</b> DTO ve aynı
        /// hesap (<c>FirmaBilgiService.Ortaklik</c>) kullanılıyor: %100 uyarısı iki ekranda
        /// farklı çıkamaz.
        /// </summary>
        public FirmaOrtaklikDto Ortaklik { get; set; } = new();

        public List<FirmaPaneliYetkiliDto> Yetkililer { get; set; } = new();

        /// <summary>İmza yetkilileri kartının başlık rozeti; yoksa <c>null</c>.</summary>
        public FirmaYetkiRozetiDto? YetkiRozeti { get; set; }

        public FirmaPaneliTakipDto Takip { get; set; } = new();

        /// <summary>Belge listesi düzenleme ekranıyla aynı DTO; görüntüleme de aynı altyapı.</summary>
        public List<FirmaBelgesiDto> Belgeler { get; set; } = new();

        public List<FirmaUyariDto> Uyarilar { get; set; } = new();

        /// <summary>Eksik künye alanları, ekran sırasıyla; listedeki rozetle aynı kaynak.</summary>
        public List<EksikAlanDto> Eksikler { get; set; } = new();
    }

    /// <summary>
    /// Anasayfa firma panelinin tek çağrılık yanıtı: <b>bütün</b> firmaların liste
    /// satırları (uyarılarıyla) + seçili firmanın ayrıntısı. Ekran açılırken firma başına
    /// ayrı istek atılmıyor.
    /// </summary>
    public class FirmaPaneliDto
    {
        public List<FirmaPaneliOzetDto> Firmalar { get; set; } = new();

        /// <summary>Firma yoksa <c>null</c>.</summary>
        public FirmaPaneliDetayDto? Secili { get; set; }

        /// <summary>Yeni firma toleransı (gün) — lejant için; değer tek yerde: <c>FirmaEksikBilgi.YeniFirmaToleransGun</c>.</summary>
        public int YeniFirmaToleransGun { get; set; } = Services.FirmaEksikBilgi.YeniFirmaToleransGun;
    }
}
