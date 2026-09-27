using System.Text.Json;
using System.Text.Json.Serialization;

namespace PkfRobot.Config;

/// <summary>
/// Bir gorev = sirali adimlar listesi.
/// Yeni is eklemek icin yeni bir JSON dosyasi yazmak yeterli, kod degismez.
/// </summary>
public class Gorev
{
    public string Ad { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public List<Adim> Adimlar { get; set; } = new();

    /// <summary>Ic ice gecebilecek en fazla <c>AltGorev</c> seviyesi.</summary>
    private const int EnCokDerinlik = 5;

    public static Gorev Yukle(string yol)
        => Yukle(yol, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);

    /// <summary>
    /// Gorevi okur ve <c>AltGorev</c> adimlarini YERINDE ACAR: alt dosyanin
    /// adimlari cagiran dosyanin adim listesine gomulur.
    ///
    /// <b>Neden acilis aninda, ayri bir adim tipi olarak degil:</b> acilmis liste
    /// duz bir adim dizisi -- adim sayaci, ilerleme yuzdesi, hiz carpani ve
    /// kalibrasyon eskisi gibi calisir. Motor <c>AltGorev</c> diye bir tip hic
    /// gormez, calisma zamaninda ic ice gorev kavrami YOKTUR.
    ///
    /// <b>Ne cozuyor:</b> "acilis" akisi 01 (ORKA'yi ac, firmaya gir) ve 02
    /// (modul gezinmesi) olarak iki dosyada duruyor ve kuyruk yalniz birini
    /// calistirabiliyordu. Adimlari ucuncu bir dosyaya KOPYALAMAK modul
    /// gezinmesini iki yerde bakilir hale getirirdi -- koordinat kopyalamanin
    /// tek-dosya-aktar.json'da acdigi bakim yukunun aynisi. Referansla
    /// birlestirmede tek kopya kaliyor.
    ///
    /// Yol, cagiran dosyanin KLASORUNE gore cozuluyor: gorevler klasoru
    /// publish'te oldugu gibi tasiniyor, mutlak yol yazilamaz.
    /// </summary>
    private static Gorev Yukle(string yol, HashSet<string> zincir, int derinlik)
    {
        if (!File.Exists(yol))
            throw new FileNotFoundException($"Gorev dosyasi bulunamadi: {yol}");

        if (derinlik > EnCokDerinlik)
            throw new InvalidOperationException(
                $"AltGorev ic ice gecme siniri asildi ({EnCokDerinlik}): {yol}");

        var tamYol = Path.GetFullPath(yol);
        if (!zincir.Add(tamYol))
            throw new InvalidOperationException(
                $"AltGorev dongusu: '{tamYol}' kendini (dolayli olarak) cagiriyor.");

        var opt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var gorev = JsonSerializer.Deserialize<Gorev>(File.ReadAllText(yol), opt)
                    ?? throw new InvalidOperationException("Gorev dosyasi okunamadi.");

        gorev.Adimlar = Ac(gorev.Adimlar, Path.GetDirectoryName(tamYol) ?? string.Empty,
                           zincir, derinlik);

        zincir.Remove(tamYol);
        return gorev;
    }

    /// <summary>AltGorev adimlarini alt dosyanin adimlariyla degistirir.</summary>
    private static List<Adim> Ac(List<Adim> adimlar, string klasor,
                                 HashSet<string> zincir, int derinlik)
    {
        // Hicbir AltGorev yoksa liste oldugu gibi kalsin: gorev dosyalarinin
        // ezici cogunlugu duz ve bu yol her calistirmada isliyor.
        if (!adimlar.Any(a => a.Tip.Trim().Equals("altgorev", StringComparison.OrdinalIgnoreCase)))
            return adimlar;

        var sonuc = new List<Adim>();

        foreach (var adim in adimlar)
        {
            if (!adim.Tip.Trim().Equals("altgorev", StringComparison.OrdinalIgnoreCase))
            {
                sonuc.Add(adim);
                continue;
            }

            if (string.IsNullOrWhiteSpace(adim.Deger))
                throw new InvalidOperationException(
                    "AltGorev adiminda 'Deger' bos: calistirilacak gorev dosyasinin adi yazilmali.");

            var altYol = Path.IsPathRooted(adim.Deger)
                ? adim.Deger
                : Path.Combine(klasor, adim.Deger);

            sonuc.AddRange(Yukle(altYol, zincir, derinlik + 1).Adimlar);
        }

        return sonuc;
    }
}

public class Adim
{
    /// <summary>
    /// Tip degerleri:
    ///   OrkaBaslat      - ORKA'yi calistir
    ///   BeklePencere    - Basligi 'Deger' iceren pencere gelene kadar bekle
    ///   Yaz             - 'Deger' metnini yaz (degisken destekler)
    ///   Tus             - 'Deger' tusuna 'Adet' kez bas (ENTER, TAB, RIGHT, ESC...)
    ///   Kisayol         - CTRL+F, CTRL+A gibi kombinasyon
    ///   Tikla           - Pencereye goreli 'X'/'Y' oranina fare ile sol tik
    ///                     ('CiftTik': true ise cift tik)
    ///                     ('OnayBekle': true ise DEVAM'a kadar bekler)
    ///   Bekle           - 'Sayi' milisaniye bekle
    ///   EkranGoruntusu  - Isimli ekran goruntusu al
    ///   Dogrula         - Aktif pencere basligi 'Deger' iceriyor mu, icermiyorsa hata
    ///   AltPencereDogrula - Gorunur bir ALT kontrolun SINIF ADI 'Deger' iceriyor mu.
    ///                     Sekme gecisleri ust seviye pencere acmadigi ve baslik
    ///                     degismedigi icin 'Dogrula' onlari goremiyor; bu adim
    ///                     EnumChildWindows + GetClassName ile bakiyor.
    ///                     'Deger' BOS ise KESIF modu: ekrandaki sinif adlarini
    ///                     log'a doker, DURDURMAZ (beklenen deger ofiste olculup
    ///                     buraya yazilacak; --probe ayni dokumu veriyor).
    ///   OnayGerekir     - DryRun ise ATLA, degilse devam (Kaydet adimlari icin)
    ///   GridDoldur      - ORKA gridine karsi hesap kodlarini yazar (veri disaridan
    ///                     verilir; liste bossa uyari yazilip adim atlanir).
    ///                     Her satir: SOL ok x'SolaGitAdet' -> TAB x'TabAdet' ->
    ///                     kod -> ENTER -> ASAGI ok. Sayilar verilmezse
    ///                     appsettings.json'daki Grid bolumunden gelir.
    ///   AltGorev        - 'Deger' adli BASKA bir gorev dosyasinin adimlarini
    ///                     buraya gomer (yol, bu dosyanin klasorune goreli).
    ///                     Yukleme aninda aciliyor: motor bu tipi hic gormez,
    ///                     adim sayaci/yuzde/kalibrasyon duz listede calisir.
    ///                     Akis birlestirmek icin -- adimlari KOPYALAMAYIN.
    ///   Log             - Log dosyasina not dus
    ///
    /// BeklePencere/Dogrula/Tikla adimlarinda 'Deger' icine '|' ile birden fazla
    /// aday baslik yazilabilir: "Veri Transferi|Transfer Islemleri"
    /// </summary>
    public string Tip { get; set; } = "";

    public string Deger { get; set; } = "";
    public int Adet { get; set; } = 1;
    public int Sayi { get; set; } = 0;
    public string Not { get; set; } = "";

    /// <summary>
    /// <c>TemizleYaz</c> adiminda: kutu NASIL bosaltilsin?
    ///
    ///   "CtrlA"     - CTRL+A ile sec, uzerine yaz. VARSAYILAN (alan bos ise bu).
    ///   "SecVeSil"  - END, SHIFT+HOME, DELETE, sonra yaz. CTRL kullanmaz.
    ///
    /// <b>Neden adim bazinda:</b> cevap ekrana gore degisiyor. ORKA'nin kendi
    /// kutularinda CTRL kombinasyonlari CALISMIYOR (08.09.2026: F7 firma arama
    /// kutusu temizlenmedi, kod eskisinin yanina yazildi, otomatik tamamlama
    /// 0001 yerine 1187'yi buldu ve robot yanlis firmada calisti). Windows'un
    /// dosya secim diyalogunda ise CTRL+A calisiyor ve {dosyaYolu}/{hesapKodu}
    /// adimlari onunla sinanmis durumda -- hepsini birden degistirmek, calisan
    /// yolu bozmak olurdu.
    ///
    /// Taninmayan deger HATA veriyor; sessizce varsayilana dusmek, bir yazim
    /// hatasinin ayni sessiz kirilmayi geri getirmesi demekti.
    /// </summary>
    public string? Temizleme { get; set; }

    /// <summary>
    /// Adet sayisini bir degiskenden al. Ornek: "modulSagOk"
    /// Komut satirindan: --degisken modulSagOk=7
    /// Degisken yoksa Adet degeri kullanilir.
    /// </summary>
    public string? AdetDegisken { get; set; }

    /// <summary>
    /// Tikla adiminda: pencerenin SOL kenarindan itibaren GENISLIGE ORANLA yatay konum.
    /// 0.0 = sol kenar, 1.0 = sag kenar. Piksel DEGIL.
    /// Oran kullanilmasinin sebebi: ekran cozunurlugu degisince piksel kayar, oran kaymaz.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Tikla adiminda: pencerenin UST kenarindan itibaren YUKSEKLIGE ORANLA dikey konum.
    /// 0.0 = ust kenar, 1.0 = alt kenar. Piksel DEGIL.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Tikla adiminda: tek tik yerine CIFT tik gonderilsin mi?
    ///
    /// Ofis denemesinde cikti: ORKA'da Banka Ekstreleri grid'indeki sablon
    /// satirina GIRMEK icin cift tik gerekiyor -- tek tik satiri yalnizca
    /// secili yapiyor, Enter da acmiyor. Adim bazinda bir alan olmasinin
    /// sebebi: gerisindeki butun Tikla adimlari tek tik ve oyle kalmali.
    /// </summary>
    public bool CiftTik { get; set; }

    /// <summary>
    /// Bu adimdan ONCE kullanici onayi beklensin mi?
    ///
    /// Gerekce: aktarim bittikten sonra KAYDET'e kullanici basiyor (robot
    /// basmiyor). Sekmeyi kapatan tiklama hemen calisirsa kullanici kaydetmeye
    /// firsat bulamadan ekran degisir. Bu bayrak konan adim, arayuzde DEVAM'a
    /// basilana kadar bekler.
    ///
    /// <b>Duraklatmayi adim motoru YAPMIYOR:</b> Calistir sekmesi bunu her adimin
    /// basindaki geri cagirmada (bkz. PkfRobot.Arayuz.GorevKosucusu) uyguluyor.
    ///
    /// <b>Gozetimsiz (ajan) yolda adim ATLANIR</b>
    /// (bkz. <c>AdimMotoru.GozetimsizAtlamaNotu</c>): duraklatacak kimse yok ve
    /// bayragin anlami "kullanici KAYDET'e basmadan bu adim calismasin". Bayrak
    /// eskiden orada sessizce yok sayiliyordu; ajan, kullanici kaydetmeden sekme
    /// kapatma tiklamasini yapardi. Konsol modlari (--gorev) elle ve basinda
    /// biriyle calistirildigi icin gozetimli sayiliyor: adim calisir.
    /// </summary>
    public bool OnayBekle { get; set; }

    /// <summary>
    /// Bu adim baslarken sunucuya bildirilecek ilerleme yuzdesi.
    ///
    /// Ilerleme kilometre taslari JSON'da duruyor, kodda degil: akisin hangi
    /// noktasinin "%25" oldugu akisin kendi bilgisi ve akis degistiginde
    /// yuzdeler de ayni dosyada degisiyor.
    /// </summary>
    public int? Yuzde { get; set; }

    /// <summary>
    /// GridDoldur adiminda: satirin 1. kolonuna donmek icin kac kez SOL ok.
    /// Verilmezse <c>appsettings.json</c>'daki <c>Grid.SolaGitAdet</c> (12).
    /// Kolon duzeni degisirse gorev dosyasindan duzeltilebilsin diye burada da var.
    /// </summary>
    public int? SolaGitAdet { get; set; }

    /// <summary>
    /// GridDoldur adiminda: 1. kolondan Karsi Hesap Kodu kolonuna kac TAB.
    /// Verilmezse <c>appsettings.json</c>'daki <c>Grid.TabAdet</c> (7).
    /// </summary>
    public int? TabAdet { get; set; }

    /// <summary>
    /// Bu adima ozel pencere bekleme suresi (saniye).
    /// Verilmezse config'deki Zamanlama.PencereTimeoutSn gecerlidir.
    /// Ornek: rapor uretimi uzun suren tek bir adim icin "TimeoutSn": 180
    /// </summary>
    public int? TimeoutSn { get; set; }
}
