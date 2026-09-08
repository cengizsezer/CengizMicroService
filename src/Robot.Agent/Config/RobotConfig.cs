using System.Text.Json;
using System.Text.Json.Serialization;

namespace PkfRobot.Config;

public class RobotConfig
{
    public string OrkaPath { get; set; } = @"C:\Orka\Orka.exe";
    public string LogKlasoru { get; set; } = @"C:\RobotLog";
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Tus/Yaz/TemizleYaz/Kisayol adimlarindan ONCE ORKA on planda mi diye bakar,
    /// degilse one getirir.
    ///
    /// Ofis testinde robot ORKA yerine cmd penceresine yazdi ve SIFRE cmd'ye gitti.
    /// Bu ayar onu engeller. Kapatmak icin ozel bir sebebin yoksa true birak.
    /// </summary>
    public bool OtomatikOneGetir { get; set; } = true;

    public GirisAyar Giris { get; set; } = new();
    public FirmaAyar Firma { get; set; } = new();
    public ZamanlamaAyar Zamanlama { get; set; } = new();
    public EkranGoruntusuAyar EkranGoruntusu { get; set; } = new();
    public PencereAyar Pencereler { get; set; } = new();
    public GridAyar Grid { get; set; } = new();

    /// <summary>
    /// Bu basliklardan biri ekranda cikarsa robot DURUR.
    /// Kapatip devam eden liste ayri: <see cref="OtomatikKapatilacakPencereler"/>.
    /// </summary>
    public List<string> BeklenmeyenPencereler { get; set; } = new();

    /// <summary>
    /// Cikinca KAPATILIP devam edilecek pencereler.
    ///
    /// <b>BeklenmeyenPencereler ile karistirilmamali:</b> o liste robotu DURDURUR
    /// ("bu ekran ciktiysa bir sey ters gitmis, kor devam etme"), bu liste pencereyi
    /// KAPATIP DEVAM EDER ("bu ekran zaten cikiyor, isle ilgisi yok").
    ///
    /// Gerekce: ORKA'nin yazici baglantisi diyalogu modul ekraninda rastgele cikiyor,
    /// odagi aliyor ve o anda gonderilen tuslari yutup zinciri kiriyordu. Durmak dogru
    /// cevap degil -- diyalog isin bir parcasi degil, yalnizca yolda duruyor.
    /// </summary>
    public List<KapatilacakPencere> OtomatikKapatilacakPencereler { get; set; } = new();

    /// <summary>Hub baglantisi (--ajan modu). ORKA otomasyonundan bagimsiz.</summary>
    public AjanAyar Ajan { get; set; } = new();

    /// <summary>Grid dolduruldiktan sonra ekrandan okuyup karsilastiran on eleme. Varsayilan KAPALI.</summary>
    public GoruntuDogrulamaAyar GoruntuDogrulama { get; set; } = new();

    public static RobotConfig Yukle(string yol)
    {
        if (!File.Exists(yol))
            throw new FileNotFoundException($"Ayar dosyasi bulunamadi: {yol}");

        var json = File.ReadAllText(yol);
        var opt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var cfg = JsonSerializer.Deserialize<RobotConfig>(json, opt)
                  ?? throw new InvalidOperationException("Ayar dosyasi okunamadi.");
        return cfg;
    }

    /// <summary>
    /// Tek bir calistirma icin kopya: <see cref="Zamanlama"/> ve <see cref="Giris"/>
    /// YENIDEN kuruluyor, geri kalan alanlar paylasiliyor.
    ///
    /// Sebep: Calistir sekmesi kendi zamanlamasi ve o firmanin sifresiyle
    /// calisiyor. Paylasilan config uzerinde oynasaydi ayni anda calisan ajan
    /// isi de o degerlerle calisir ve bunu kimse fark etmezdi.
    /// </summary>
    public RobotConfig CalismaKopyasi() => new()
    {
        OrkaPath = OrkaPath,
        LogKlasoru = LogKlasoru,
        DryRun = DryRun,
        OtomatikOneGetir = OtomatikOneGetir,
        Firma = Firma,
        EkranGoruntusu = EkranGoruntusu,
        Pencereler = Pencereler,
        BeklenmeyenPencereler = BeklenmeyenPencereler,
        OtomatikKapatilacakPencereler = OtomatikKapatilacakPencereler,
        Ajan = Ajan,
        GoruntuDogrulama = GoruntuDogrulama,

        Giris = new GirisAyar
        {
            Veritabani = Giris.Veritabani,
            Kullanici = Giris.Kullanici,
            Sifre = Giris.Sifre,
            FirmaSifresi = Giris.FirmaSifresi
        },
        Zamanlama = new ZamanlamaAyar
        {
            AdimBeklemeMs = Zamanlama.AdimBeklemeMs,
            TusBeklemeMs = Zamanlama.TusBeklemeMs,
            PencereTimeoutSn = Zamanlama.PencereTimeoutSn,
            OrkaAcilisTimeoutSn = Zamanlama.OrkaAcilisTimeoutSn
        }
    };
}

public class GirisAyar
{
    public string Veritabani { get; set; } = "";
    public string Kullanici { get; set; } = "";
    public string Sifre { get; set; } = "";

    /// <summary>
    /// ORKA ikinci kez sifre sorar (firma acilirken). Goreve {firmaSifre} olarak gecer.
    /// Oncelik: --degisken firmaSifre=xxx > ORKA_FIRMA_SIFRE ortam degiskeni > bu alan.
    /// </summary>
    public string FirmaSifresi { get; set; } = "";
}

public class FirmaAyar
{
    public string Kod { get; set; } = "";
    public string BaslikDogrulama { get; set; } = "";
}

public class ZamanlamaAyar
{
    public int AdimBeklemeMs { get; set; } = 700;
    public int TusBeklemeMs { get; set; } = 150;
    public int PencereTimeoutSn { get; set; } = 40;
    public int OrkaAcilisTimeoutSn { get; set; } = 90;
}

public class EkranGoruntusuAyar
{
    public bool HerAdimda { get; set; } = true;
    public bool HataDurumunda { get; set; } = true;
}

/// <summary>
/// Ajanin sunucuya baglanma ayarlari.
///
/// Adresler koda gomulmedi: yayin adresi degistiginde ya da bir seyi yerelde
/// denemek gerektiginde Notepad yetsin, yeniden derleme gerekmesin -- projedeki
/// appsettings disiplininin ayni gerekcesi.
/// </summary>
public class AjanAyar
{
    /// <summary>Anahtari token'a ceviren uc.</summary>
    public string TokenUcu { get; set; } = "https://www.dijitalmasraf.com/auth/agent/token";

    /// <summary>Hub adresi. wss:// yazilabilir; istemci https://'e cevirir.</summary>
    public string HubAdresi { get; set; } = "https://www.dijitalmasraf.com/agenthub";

    /// <summary>
    /// Kalp atisi araligi. Sunucunun zaman asimi 90 saniye; 30 saniye, tek bir
    /// kacan atisin ajani listeden dusurmemesi icin secildi.
    /// </summary>
    public int KalpAtisiSaniye { get; set; } = 30;

    /// <summary>Token'in kalan omru bunun altina inince yenilenir.</summary>
    public int TokenYenilemeEsigiDakika { get; set; } = 30;

    /// <summary>Ajan log dosyalari bu gunden eskiyse silinir.</summary>
    public int LogSaklamaGun { get; set; } = 14;

    /// <summary>ORKA'nin surec adi (uzantisiz). OrkaPath ile ayni exe olmali.</summary>
    public string OrkaSurecAdi { get; set; } = "OrkaWinIceberg.64";

    /// <summary>
    /// Is dosyalarinin indirildigi kok (<c>{kok}/is/{isId}/ekstre</c> ve
    /// <c>/kod-listesi</c>). Ajan yalnizca kendi isinin dosyalarini alabiliyor.
    /// </summary>
    public string IsUcuKoku { get; set; } = "https://www.dijitalmasraf.com/catalog/agent";

    /// <summary>Hata ekraninin yuklendigi uc (FileApiService, genel yukleme).</summary>
    public string DosyaYuklemeUcu { get; set; } = "https://www.dijitalmasraf.com/file/v1/uploads";
}

/// <summary>
/// Kendiliginden kapatilacak bir pencere kurali.
/// </summary>
public class KapatilacakPencere
{
    /// <summary>Pencere basliginda ARANAN parca (icerir mantigi, buyuk/kucuk harf duyarsiz).</summary>
    public string Baslik { get; set; } = "";

    /// <summary>
    /// Basilacak dugmenin yazisi ("Iptal", "Hayir", "Kapat").
    /// Bos birakilirsa ya da dugme bulunamazsa ESC gonderilir: ORKA'nin diyaloglari
    /// UIA'ya kapali olabiliyor ve o zaman elde kalan tek yol klavye.
    /// </summary>
    public string Dugme { get; set; } = "";
}

/// <summary>
/// ORKA gridinde KLAVYEYLE gezinme sayilari.
///
/// <b>Neden ayarlanabilir:</b> hedef hucreye kac TAB uzakta olundugu ORKA'nin
/// kolon duzenine bagli ve o duzen surumle degisebiliyor. Sayilar koda gomulu
/// olsaydi kolon eklenen bir ORKA guncellemesi yeni bir yayin gerektirirdi;
/// burada durunca appsettings.json'dan, tek bir adim icin de gorev JSON'undan
/// duzeltilebiliyor.
/// </summary>
public class GridAyar
{
    /// <summary>
    /// Satirin 1. kolonuna donmek icin kac kez SOL ok.
    ///
    /// Kolon sayisindan FAZLA olmasi kasitli: sol ok ilk kolonda etkisiz kalir,
    /// yani fazlasi zararsiz ama eksigi hedefi kaydirir. "Hangi kolondaydik"
    /// sorusunu sormadan sabit bir baslangica donmenin tek yolu bu -- grid
    /// UIA'ya kapali, imlecin nerede oldugu okunamiyor.
    /// </summary>
    public int SolaGitAdet { get; set; } = 12;

    /// <summary>1. kolondan Karsi Hesap Kodu kolonuna kac TAB (ofiste olculdu).</summary>
    public int TabAdet { get; set; } = 7;
}

public class PencereAyar
{
    public string GirisEkrani { get; set; } = "";
    public string SubeSecim { get; set; } = "";
    public string AnaEkran { get; set; } = "ORKA_";
    public string DosyaSecim { get; set; } = "";
    public string HesapPlani { get; set; } = "";
}

/// <summary>
/// GridDoldur'dan sonra ekran goruntusunu okuyup yazilmasi beklenen kod
/// listesiyle karsilastiran katmanin ayarlari.
///
/// <b>Varsayilan KAPALI ve oyle kalmali.</b> Kapaliyken hicbir API cagrisi
/// yapilmiyor, anahtar aranmiyor, mevcut akis bit bit ayni kaliyor -- ozellik
/// acilana kadar var olmamis gibi davraniyor. Bu bir ON ELEME: hicbir kosulda
/// robotu durdurmuyor, yalnizca log'a "su satirlara bak" yaziyor. Kaydet'e
/// zaten kullanici basiyor.
///
/// <b>Goruntudeki veri disari cikiyor:</b> ekran goruntusunde musterinin banka
/// hareket aciklamalari var ve acikken bu goruntu Anthropic API'sine
/// gonderiliyor. Ayari acmak bilincli bir karar olmali.
/// </summary>
public class GoruntuDogrulamaAyar
{
    public bool Aktif { get; set; } = false;

    /// <summary>
    /// Okumayi yapan model. Istenen sey muhasebe karari degil, ekrandaki metni
    /// okumak; yine de kucuk puntolu grid'de yanlis okunan bir rakam bosuna
    /// uyari uretir, o yuzden varsayilan en yetenekli model.
    /// </summary>
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// Cagri bu surede bitmezse dogrulama atlaniyor. Kisa tutuluyor: robot
    /// ORKA'yi ekranda bekletiyor ve dogrulama bir yardimci, isin kendisi degil.
    /// </summary>
    public int ZamanAsimiSaniye { get; set; } = 60;
}
