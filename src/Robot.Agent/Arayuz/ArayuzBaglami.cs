using System.Runtime.Versioning;
using PkfRobot.Ajan;
using PkfRobot.Ayarlar;
using PkfRobot.Config;

namespace PkfRobot.Arayuz;

/// <summary>
/// Arayuzun paylastigi durum: ayarlar, depolar, ajan koprusu, log.
///
/// Paneller birbirini tanimiyor; hepsi bunu taniyor. Ayar bir yerde
/// degistiginde digerlerinin de gormesi bu sayede -- ornegin ORKA exe yolu
/// Ayarlar'da degisince Kalibrasyon'daki "Dene" ayni ORKA'yi buluyor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ArayuzBaglami : IDisposable
{
    public ArayuzBaglami(RobotConfig cfg, string ayarKlasoru, string gorevlerKlasoru,
                         Func<string, string?> anahtarSor)
    {
        Config = cfg;
        GorevlerKlasoru = gorevlerKlasoru;

        AyarDeposu = new AyarDeposu(ayarKlasoru);
        SifreDeposu = new SifreDeposu(ayarKlasoru);

        Ayarlar = AyarTanimlari.VarsayilanlariTamamla(AyarDeposu.Oku());
        Sifreler = SifreDeposu.Oku();

        Izleyici = new IsIzleyici();

        // Dosya log'u ajanin kendisi aciyor; burada yalniz ekrana giden kopya
        // kuruluyor ve ajan basladiginda dosya agzi ona takiliyor.
        Log = new CiftYonluLog();

        Kopru = new AjanKoprusu(cfg, Log, Izleyici, anahtarSor);
        Orka = new OrkaPenceresi(cfg.Ajan.OrkaSurecAdi);
    }

    public RobotConfig Config { get; }
    public string GorevlerKlasoru { get; }

    public AyarDeposu AyarDeposu { get; }
    public SifreDeposu SifreDeposu { get; }

    public RobotAyarlari Ayarlar { get; private set; }
    public Sifreler Sifreler { get; private set; }

    public IsIzleyici Izleyici { get; }
    public CiftYonluLog Log { get; }
    public AjanKoprusu Kopru { get; }
    public OrkaPenceresi Orka { get; }

    /// <summary>Ayarlar diskte degisti; paneller kendini tazelesin.</summary>
    public event Action? AyarlarDegisti;

    public void AyarlariKaydet()
    {
        AyarDeposu.Yaz(Ayarlar);
        CalisirYollariHazirla();
        AyarlarDegisti?.Invoke();
    }

    public void SifreleriKaydet(Sifreler sifreler)
    {
        Sifreler = sifreler;
        SifreDeposu.Yaz(sifreler);
    }

    /// <summary>Yedegi geri yukledikten sonra bellekteki kopyayi da tazeler.</summary>
    public RobotAyarlari YedektenYukle(string dosya)
    {
        Ayarlar = AyarTanimlari.VarsayilanlariTamamla(AyarDeposu.GeriYukle(dosya));
        AyarlarDegisti?.Invoke();
        return Ayarlar;
    }

    /// <summary>
    /// Kayitli kalibrasyonu gorev dosyalarina yazar.
    ///
    /// Uygulama her acilista bunu cagiriyor: publish gorev dosyalarinin uzerine
    /// yaziyor ve oradaki koordinatlar varsayilanlarina donuyor. Asil kopya
    /// <c>%AppData%</c>'da durdugu icin kalibrasyon kendiliginden geri geliyor.
    /// </summary>
    public UygulamaRaporu KalibrasyonuUygula()
        => KalibrasyonUygulama.Uygula(GorevlerKlasoru, Ayarlar.Koordinatlar);

    /// <summary>
    /// Ayarlarda secilen yollari <see cref="RobotConfig"/>'e tasir: ajan ve adim
    /// motoru config'i okuyor, arayuz ayarlarini degil. Ikisini ayri tutmak,
    /// kullanicinin ekranda degistirdigi ORKA yolunun ise etki etmemesi demek
    /// olurdu.
    /// </summary>
    public void CalisirYollariHazirla()
    {
        if (!string.IsNullOrWhiteSpace(Ayarlar.OrkaExeYolu))
            Config.OrkaPath = Ayarlar.OrkaExeYolu;

        if (!string.IsNullOrWhiteSpace(Ayarlar.LogKlasoru))
            Config.LogKlasoru = Ayarlar.LogKlasoru;

        if (!string.IsNullOrWhiteSpace(Ayarlar.FirmaKodu))
            Config.Firma.Kod = Ayarlar.FirmaKodu;

        if (!string.IsNullOrWhiteSpace(Ayarlar.KullaniciKodu))
            Config.Giris.Kullanici = Ayarlar.KullaniciKodu;

        if (!string.IsNullOrEmpty(Sifreler.OrkaSifresi))
            Config.Giris.Sifre = Sifreler.OrkaSifresi;

        if (!string.IsNullOrEmpty(Sifreler.FirmaSifresi))
            Config.Giris.FirmaSifresi = Sifreler.FirmaSifresi;

        KlasorAc(Ayarlar.IsDosyalariKlasoru);
        KlasorAc(Ayarlar.LogKlasoru);
    }

    private static void KlasorAc(string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol)) return;

        try
        {
            Directory.CreateDirectory(yol);
        }
        catch (Exception)
        {
            // Klasor acilamiyorsa uyari zaten Ayarlar ekraninda kirmizi
            // gorunuyor; burada patlamak arayuzu acilmaz yapardi.
        }
    }

    public void Dispose() => Kopru.Dispose();
}
