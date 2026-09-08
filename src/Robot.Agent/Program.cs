using System.Globalization;
using System.Text;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using PkfRobot.Ajan;
using PkfRobot.Arayuz;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot;

public static class Program
{
    /// <summary>
    /// STA sart: WinForms diyaloglari (dosya secici, klasor secici, pano)
    /// yalniz tek is parcacikli apartmanda dogru calisiyor. Konsol modlari
    /// bundan etkilenmiyor.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            var p = Parametreler.Coz(args);

            if (p.Yardim)
            {
                YardimYazdir();
                return 0;
            }

            // Anahtar kaydi config'ten ONCE: anahtari girmek icin appsettings.json'un
            // okunabilir olmasi gerekmiyor ve ozelligin acik olmasi da gerekmiyor.
            if (p.AnthropicAnahtariAyarla)
                return AnthropicAnahtariniAyarla();

            var cfgYolu = p.ConfigYolu ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var cfg = RobotConfig.Yukle(cfgYolu);

            // --- ARAYUZ: argumansiz calistirma. Konsol modlari aynen duruyor. ---
            if (p.Arayuz)
                return ArayuzCalistir(cfg);

            // --- AJAN MODU: hub'a bagli kal. ORKA'ya dokunmaz, gorev calistirmaz. ---
            if (p.Ajan)
                return AjanCalistir(cfg, p);

            // Komut satiri config'i ezer
            if (p.FirmaKodu != null) cfg.Firma.Kod = p.FirmaKodu;
            if (p.CanliMod) cfg.DryRun = false;

            // Sifre oncelik sirasi: parametre > ortam degiskeni > appsettings.json
            // Komut satirindaki sifre cmd penceresinin BASLIGINDA gorunur ve ekran
            // goruntulerine duser. Ortam degiskeni bu yuzden tercih edilmeli.
            // ACIKCA verilen degerler ayrica tutuluyor: bunlar firma bazli depoyu
            // da EZMELI. Kullanici --sifre yazdiysa kastettigi sey odur; depodaki
            // kayit "bu firmanin sifresi" varsayimidir ve acik istegin altinda kalir.
            var acikOrkaSifresi = SifreCoz(p.Sifre, "ORKA_SIFRE", string.Empty);
            var acikFirmaSifresi = SifreCoz(null, "ORKA_FIRMA_SIFRE", string.Empty);

            cfg.Giris.Sifre = SifreCoz(p.Sifre, "ORKA_SIFRE", cfg.Giris.Sifre);
            cfg.Giris.FirmaSifresi = SifreCoz(null, "ORKA_FIRMA_SIFRE", cfg.Giris.FirmaSifresi);

            using var automation = new UIA3Automation();

            // --- PROBE MODU: hicbir tusa basma, sadece ekrandakileri dok ---
            if (p.Probe)
                return ProbeCalistir(cfg, automation);

            // --- KALIBRE MODU: fareyi hedefe getir, oranini oku ---
            if (p.Kalibre)
                return KalibreCalistir(cfg, automation);

            if (p.GorevYolu == null)
            {
                Console.WriteLine("HATA: --gorev parametresi zorunlu. --yardim ile kullanimi gor.");
                return 2;
            }

            var gorev = Gorev.Yukle(p.GorevYolu);

            using var log = new AdimLogger(cfg.LogKlasoru, gorev.Ad,
                                           cfg.EkranGoruntusu.HerAdimda);

            log.Bilgi($"Config: {cfgYolu}");
            log.Bilgi($"Gorev : {p.GorevYolu}");
            log.Bilgi($"Firma : {cfg.Firma.Kod}");
            log.Bilgi($"DryRun: {cfg.DryRun}");

            // Sifreler firma bazli depodan; appsettings/ortam degiskeni YEDEK.
            // Ajan yolu ve Calistir sekmesi de ayni cozucuyu kullaniyor: uc yol
            // ayni firmaya ayni sifreyi denesin (bkz. FirmaSifreCozucu).
            var sifreCozum = PkfRobot.Ayarlar.FirmaSifreCozucu.Coz(
                SifreDeposunuOku(log), cfg.Firma.Kod, cfg.Giris.Sifre, cfg.Giris.FirmaSifresi);

            var orkaSifresi = Ustunlu(acikOrkaSifresi, sifreCozum.Orka);
            var firmaSifresi = Ustunlu(acikFirmaSifresi, sifreCozum.Firma);

            log.Bilgi($"Firma {cfg.Firma.Kod} ORKA giris sifresi: " +
                      $"{Kaynak(acikOrkaSifresi, sifreCozum.Orka)}.");
            log.Bilgi($"Firma {cfg.Firma.Kod} firma sifresi: " +
                      $"{Kaynak(acikFirmaSifresi, sifreCozum.Firma)}.");

            if (orkaSifresi.Length == 0 || firmaSifresi.Length == 0)
                log.Uyari(sifreCozum.EksikMesaji + " (--sifre / ORKA_SIFRE ile de verilebilir.)");

            var degiskenler = new Dictionary<string, string>
            {
                ["firmaKodu"]  = cfg.Firma.Kod,
                ["kullanici"]  = cfg.Giris.Kullanici,
                ["veritabani"] = cfg.Giris.Veritabani,
                ["sifre"]      = orkaSifresi,
                ["firmaSifre"] = firmaSifresi,
                ["donem"]      = DateTime.Now.ToString("yyyyMM")
            };

            // --degisken ad=deger ile ekstra degisken gecilebilir.
            // Sozluk kurulduktan SONRA calisir, yani firmaSifre dahil her seyi ezer.
            foreach (var (k, v) in p.EkDegiskenler)
                degiskenler[k] = v;

            var motor = new AdimMotoru(cfg, log, automation, degiskenler);
            motor.Calistir(gorev);

            Console.WriteLine();
            Console.WriteLine($"BASARILI. Log klasoru: {log.Klasor}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("=== HATA ===");
            Console.WriteLine(ex.Message);
            Console.WriteLine();
            Console.WriteLine("Detay:");
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }

    /// <summary>
    /// Masaustu arayuzu. Argumansiz calistirmanin karsiligi.
    ///
    /// Konsol penceresi gizleniyor: exe konsol alt sisteminde derlendigi icin
    /// (kasitli -- <c>--ajan</c> ciktisi gorunsun diye) arayuzun arkasinda bos
    /// bir siyah pencere kalirdi.
    /// </summary>
    private static int ArayuzCalistir(RobotConfig cfg)
    {
        KonsoluGizle();

        ApplicationConfiguration.Initialize();

        var kok = AjanKimlikDeposu.VarsayilanKlasor;
        var gorevler = Path.Combine(AppContext.BaseDirectory, "gorevler");

        // Anahtar sorusu ajanin arka plan is parcacigindan geliyor; pencere
        // acmadan once arayuz is parcacigina gecmek zorunlu.
        Form? pencere = null;
        string? AnahtarSor(string klasor)
            => pencere is { IsHandleCreated: true } p
                ? (string?)p.Invoke(new Func<string?>(() => AnahtarFormu.Sor(klasor)))
                : AnahtarFormu.Sor(klasor);

        using var baglam = new ArayuzBaglami(cfg, kok, gorevler, AnahtarSor);
        using var form = new AnaForm(baglam);

        pencere = form;
        Application.Run(form);
        return 0;
    }

    private static void KonsoluGizle()
    {
        try
        {
            var konsol = Arayuz.KonsolPenceresi.Tutamac();
            if (konsol != IntPtr.Zero) Arayuz.KonsolPenceresi.Gizle(konsol);
        }
        catch (Exception)
        {
            // Konsol yoksa (baska bir surecten baslatilmis) yapacak bir sey yok.
        }
    }

    /// <summary>
    /// Ajan modu: hub'a baglanip bagli kalir. Ctrl+C ile duzgun kapanir --
    /// sureci aniden oldurmek, sunucuda kalp atisi zaman asimina kadar duracak
    /// hayalet bir kayit birakmak demek.
    /// </summary>
    private static int AjanCalistir(RobotConfig cfg, Parametreler p)
    {
        using var iptal = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;   // sureci hemen kapatma, dongu kendisi cikacak
            iptal.Cancel();
        };

        return AjanCalistirici.CalistirAsync(cfg, p.AnahtariSifirla, iptal.Token)
                              .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Goruntu dogrulamasinin API anahtarini sorar ve DPAPI ile kaydeder.
    ///
    /// Ayri bir komut olmasinin sebebi ajan anahtariyla ayni: anahtar
    /// <c>appsettings.json</c>'a YAZILMIYOR (o dosya publish ile uzerine
    /// yaziliyor ve depoya giriyor). Girdi ekranda gorunmuyor -- robotun aldigi
    /// ekran goruntulerine dusmesin.
    /// </summary>
    private static int AnthropicAnahtariniAyarla()
    {
        var kok = AjanKimlikDeposu.VarsayilanKlasor;
        var depo = new AnthropicAnahtarDeposu(kok);

        Console.WriteLine();
        Console.WriteLine("=== Goruntu dogrulama API anahtari ===");
        Console.WriteLine($"Kaydedilecek yer: {depo.Dosya} (DPAPI ile sifreli)");
        Console.WriteLine("Anahtar ekranda gorunmeyecek. Bos birakip Enter'a basmak KAYITLIYI SILER.");
        Console.Write("Anthropic API anahtari: ");

        var anahtar = GizliOku();
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(anahtar))
        {
            depo.Sil();
            Console.WriteLine("Anahtar girilmedi; kayitli anahtar silindi. Dogrulama yapilmayacak.");
            return 0;
        }

        depo.Yaz(anahtar);
        Console.WriteLine("Anahtar kaydedildi.");
        Console.WriteLine("Ozelligi acmak icin appsettings.json > GoruntuDogrulama.Aktif = true yapin.");
        Console.WriteLine("DIKKAT: acikken ekran goruntusu (musteri hareket aciklamalari dahil) " +
                          "Anthropic API'sine gonderilir.");
        return 0;
    }

    /// <summary>
    /// Girdiyi ekrana yazmadan okur. <see cref="AjanCalistirici"/> icindeki
    /// esinin ayrisi: orasi ajan anahtarini kendi akisinda soruyor, burasi
    /// gorev calistirmayan tek seferlik bir komut.
    /// </summary>
    private static string GizliOku()
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? string.Empty;

        var girilen = new System.Text.StringBuilder();

        while (true)
        {
            var tus = Console.ReadKey(intercept: true);

            if (tus.Key == ConsoleKey.Enter) break;

            if (tus.Key == ConsoleKey.Backspace)
            {
                if (girilen.Length > 0)
                {
                    girilen.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }

            if (char.IsControl(tus.KeyChar)) continue;

            girilen.Append(tus.KeyChar);
            Console.Write('*');
        }

        return girilen.ToString();
    }

    /// <summary>Acikca verilen deger varsa o, yoksa depodan cozulen.</summary>
    private static string Ustunlu(string acik, PkfRobot.Ayarlar.SifreDegeri cozulen)
        => acik.Length > 0 ? acik : cozulen.Deger;

    /// <summary>Log satiri icin kaynak adi; <b>sifrenin kendisi yazilmiyor.</b></summary>
    private static string Kaynak(string acik, PkfRobot.Ayarlar.SifreDegeri cozulen)
        => acik.Length > 0 ? "--sifre parametresi / ortam degiskeni" : cozulen.KaynakAdi;

    /// <summary>
    /// Firma bazli sifre deposu; okunamazsa bos doner ve yalnizca yedekler kalir.
    ///
    /// <b>Neden konsol yolu da okuyor:</b> arayuzden girilen sifreler zaten bu
    /// dosyada. Konsol yolu yalnizca appsettings'e baksaydi ayni makinede
    /// "arayuzden calisiyor, --gorev ile calismiyor" durumu cikardi.
    /// </summary>
    private static PkfRobot.Ayarlar.Sifreler SifreDeposunuOku(AdimLogger log)
    {
        try
        {
            return new PkfRobot.Ayarlar.SifreDeposu(
                PkfRobot.Ayarlar.AyarDeposu.VarsayilanKlasor).Oku();
        }
        catch (Exception ex)
        {
            log.Uyari($"Firma bazli sifre deposu okunamadi: {ex.Message}. " +
                      "appsettings.json > Giris yedegine bakilacak.");
            return new PkfRobot.Ayarlar.Sifreler();
        }
    }

    /// <summary>
    /// Sifreyi oncelik sirasina gore secer:
    ///   1. Komut satiri parametresi (--sifre)
    ///   2. Ortam degiskeni (ORKA_SIFRE / ORKA_FIRMA_SIFRE)
    ///   3. appsettings.json
    /// Komut satirindaki sifre baslik cubugunda gorunur; ortam degiskeni gorunmez.
    /// </summary>
    private static string SifreCoz(string? parametre, string ortamDegiskeni, string configDegeri)
    {
        if (!string.IsNullOrEmpty(parametre))
            return parametre;

        var ortam = Environment.GetEnvironmentVariable(ortamDegiskeni);
        if (!string.IsNullOrEmpty(ortam))
            return ortam;

        return configDegeri;
    }

    /// <summary>
    /// Canli oran okuyucu. Fareyi hedefin uzerine getir, ekrandaki orani JSON'a yapistir.
    /// Saniyede bir okur, Ctrl+C'ye kadar durmaz.
    ///
    /// Neden: her olcum icin gorev calistirip log klasorune bakmak yavas.
    /// </summary>
    private static int KalibreCalistir(RobotConfig cfg, UIA3Automation automation)
    {
        using var log = new AdimLogger(cfg.LogKlasoru, "kalibre", false);
        var bekleyici = new PencereBekleyici(automation, log, cfg);

        var devam = true;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; devam = false; };

        Console.WriteLine();
        Console.WriteLine("=== KALIBRE MODU ===");
        Console.WriteLine($"Olculen pencere: basligi '{cfg.Pencereler.AnaEkran}' iceren pencere");
        Console.WriteLine("Fareyi hedefin uzerine getir, asagidaki orani JSON'a yapistir.");
        Console.WriteLine("Cikmak icin Ctrl+C.");
        Console.WriteLine();

        var oncekiPencereYok = false;
        var sessizTur = 0;

        while (devam)
        {
            var nokta = Mouse.Position;
            var pencere = bekleyici.Bul(cfg.Pencereler.AnaEkran);

            if (pencere == null)
            {
                // Uyariyi her saniye basmak ekrani doldurur; durum degisiminde ve
                // arada bir hatirlatma olarak yaz. Program CIKMAZ, beklemeye devam eder.
                if (!oncekiPencereYok || sessizTur >= 10)
                {
                    Console.WriteLine(
                        $"UYARI: '{cfg.Pencereler.AnaEkran}' iceren pencere yok. " +
                        $"Fare: ({nokta.X}, {nokta.Y}). Bekleniyor...");
                    sessizTur = 0;
                }
                oncekiPencereYok = true;
                sessizTur++;
                Thread.Sleep(1000);
                continue;
            }

            if (oncekiPencereYok)
            {
                Console.WriteLine($"Pencere bulundu: '{pencere.Baslik}'");
                oncekiPencereYok = false;
                sessizTur = 0;
            }

            // Olcu tek kaynaktan: OrkaPenceresi.OlcuAl (Win32 GetWindowRect).
            // Kalibre modunun okudugu dikdortgen ile AdimMotoru.Tikla'nin tikladigi
            // dikdortgen ayni olmali; UIA BoundingRectangle ile ayrisiyordu.
            var r = OrkaPenceresi.OlcuAl(pencere.Tutamac);
            if (!r.Gecerli)
            {
                Console.WriteLine("UYARI: Pencere olculeri okunamadi (simge durumunda olabilir).");
                Thread.Sleep(1000);
                continue;
            }

            // Sol/Ust negatif olabilir (ikinci monitor, DWM kenarlik payi).
            var oranX = (nokta.X - r.Sol) / (double)r.Genislik;
            var oranY = (nokta.Y - r.Ust) / (double)r.Yukseklik;
            var disarida = oranX is < 0 or > 1 || oranY is < 0 or > 1;

            // Ondalik ayrac her zaman NOKTA olmali; Turkce locale'de virgul basar
            // ve JSON'a yapistirildiginda bozuk olur.
            var sx = oranX.ToString("0.000", CultureInfo.InvariantCulture);
            var sy = oranY.ToString("0.000", CultureInfo.InvariantCulture);

            Console.WriteLine(
                $"X: {sx}  Y: {sy}   (mutlak: {nokta.X}, {nokta.Y})   " +
                $"\"X\": {sx}, \"Y\": {sy}" +
                (disarida ? "   << FARE PENCERENIN DISINDA" : ""));

            Thread.Sleep(1000);
        }

        Console.WriteLine();
        Console.WriteLine("Kalibre modu kapatildi.");
        return 0;
    }

    /// <summary>
    /// Hicbir tusa basmadan ekrandaki pencereleri listeler.
    /// Ofiste bir sey beklendigi gibi cikmazsa once bunu calistir.
    /// </summary>
    private static int ProbeCalistir(RobotConfig cfg, UIA3Automation automation)
    {
        using var log = new AdimLogger(cfg.LogKlasoru, "probe", true);
        var bekleyici = new PencereBekleyici(automation, log, cfg);

        log.Bilgi("PROBE MODU - hicbir tusa basilmiyor, sadece okunuyor.");
        log.Bilgi("");
        log.Bilgi("--- Ekrandaki pencereler ---");

        var basliklar = bekleyici.TumPencereBasliklari();
        if (basliklar.Count == 0)
            log.Uyari("Hic pencere okunamadi. ORKA yonetici modunda calisiyor olabilir; " +
                      "bu exe'yi de yonetici olarak calistirmayi dene.");

        foreach (var b in basliklar)
            log.Bilgi($"  > {b}");

        log.Bilgi("");
        log.Bilgi("--- Config'deki beklenen basliklar kontrolu ---");
        Kontrol(log, bekleyici, "GirisEkrani", cfg.Pencereler.GirisEkrani);
        Kontrol(log, bekleyici, "SubeSecim",   cfg.Pencereler.SubeSecim);
        Kontrol(log, bekleyici, "AnaEkran",    cfg.Pencereler.AnaEkran);
        Kontrol(log, bekleyici, "DosyaSecim",  cfg.Pencereler.DosyaSecim);
        Kontrol(log, bekleyici, "HesapPlani",  cfg.Pencereler.HesapPlani);

        // ORKA'nin ic kontrolleri UIA'ya kapali; sekme/modul gecisi ust seviye
        // pencere ACMIYOR ve baslik da degismiyor. Okunabilen tek yapisal sinyal
        // alt pencere agaci ve sinif adlari. Bu dokumu IKI kez al -- once modul
        // ekraninda, sonra Veri Transferi acikken -- ve farki karsilastir: cikan
        // sinif adi 'AltPencereDogrula' adiminin Deger alanina yazilacak.
        log.Bilgi("");
        log.Bilgi("--- ORKA alt pencereleri (gorunur, sinif adina gore) ---");

        var ozet = bekleyici.AltPencereOzeti(60);
        if (ozet.Count == 0)
            log.Uyari("Hic alt kontrol okunamadi. ORKA acik degil ya da yonetici modunda calisiyor.");

        foreach (var satir in ozet)
            log.Bilgi($"  # {satir}");

        log.Bilgi("");
        log.Bilgi("--- Metni OLAN gorunur alt kontroller (sinif <- metin) ---");

        var metinliler = bekleyici.OrkaAltPencereleri()
            .Where(a => a.Gorunur && !string.IsNullOrWhiteSpace(a.Baslik))
            .Take(80)
            .ToList();

        if (metinliler.Count == 0)
            log.Bilgi("  (metni okunabilen gorunur kontrol yok)");

        foreach (var a in metinliler)
            log.Bilgi($"  # d{a.Derinlik,-2} {a.SinifAdi,-32} <- '{a.Baslik}'");

        log.EkranAl("probe-ekran", zorla: true);

        Console.WriteLine();
        Console.WriteLine($"Probe bitti. Log klasoru: {log.Klasor}");
        return 0;
    }

    private static void Kontrol(AdimLogger log, PencereBekleyici b, string ad, string deger)
    {
        if (string.IsNullOrWhiteSpace(deger)) return;
        var var_mi = b.VarMi(deger);
        log.Bilgi($"  {ad,-14} '{deger}' -> {(var_mi ? "BULUNDU" : "yok")}");
    }

    private static void YardimYazdir()
    {
        Console.WriteLine(@"
PkfRobot - ORKA masaustu otomasyon ajani

KULLANIM:
  PkfRobot.exe                        (argumansiz: masaustu arayuzunu acar)
  PkfRobot.exe --ajan
  PkfRobot.exe --gorev gorevler\01-orka-ac-firma-sec.json --sifre GIZLI
  PkfRobot.exe --probe
  PkfRobot.exe --kalibre
  PkfRobot.exe --gorev ... --firma 0004 --canli

PARAMETRELER:
  --ajan              Sunucuya baglanir ve bagli kalir (kalp atisi + ORKA durumu).
                      Ilk calistirmada ajan anahtarini sorar ve DPAPI ile
                      %AppData%\PkfRobot\agent.dat icine sifreli kaydeder.
                      Ctrl+C ile durur. ORKA'ya dokunmaz, gorev calistirmaz.
  --anahtari-sifirla  Kayitli ajan anahtarini siler, yenisini sorar
                      (ajan modunu kendisi acar)
  --anthropic-anahtari-ayarla
                      Grid dogrulamasinin API anahtarini sorar ve DPAPI ile
                      %AppData%\PkfRobot\anthropic.dat icine kaydeder, cikar.
                      Bos Enter kayitli anahtari siler. Ozelligin kendisi
                      appsettings.json > GoruntuDogrulama.Aktif ile aciliyor.
  --gorev <yol>       Calistirilacak gorev JSON dosyasi
  --config <yol>      Ayar dosyasi (varsayilan: appsettings.json)
  --firma <kod>       Firma kodunu ez (or: 0001)
  --sifre <sifre>     ORKA giris sifresi
  --probe             Hicbir tusa basmaz, ekrandaki pencereleri VE ORKA'nin
                      alt kontrollerini (sinif adlariyla) listeler
  --kalibre           Fare konumunun ORKA penceresine goreli oranini canli yazar
                      (Tikla adiminin X/Y degerlerini olcmek icin, Ctrl+C ile cikilir)
  --canli             DryRun'i KAPATIR, gercek kayit yapar. DIKKAT.
  --degisken ad=deger Goreve ekstra degisken gecer (birden fazla olabilir)
  --yardim            Bu ekran

ARAYUZ:
  Argumansiz calistirildiginda kucuk bir pencere acilir: baglanti durumu,
  calisan is, son bes is, log; ayarlar (yollar, ORKA giris bilgileri, sifreler)
  ve koordinat kalibrasyonu. Kapatma dugmesi tepsiye indirir, cikis tepsi
  menusunden. Ayarlar %AppData%\PkfRobot\ayarlar.json icinde durur.

SIFRELER (oncelik sirasi yukaridan asagiya):
  1. --sifre parametresi                        (firma sifresi: --degisken firmaSifre=xxx)
  2. ORKA_SIFRE ortam degiskeni                 (firma sifresi: ORKA_FIRMA_SIFRE)
  3. appsettings.json > Giris.Sifre / Giris.FirmaSifresi

  DIKKAT: Komut satirina yazilan sifre cmd penceresinin BASLIGINDA gorunur ve
  robotun aldigi ekran goruntulerine duser. Tercihen ortam degiskeni kullan:

    set ORKA_SIFRE=xxx
    set ORKA_FIRMA_SIFRE=yyy
    PkfRobot.exe --gorev gorevler\01-orka-ac-firma-sec.json

AJAN MODU:
  Anahtar DijitalMasraf > Yonetim > Ajanlar ekranindan alinir (pkfr_ ile baslar).
  Bir kez girilir; %AppData%\PkfRobot altinda sifreli durur, publish uzerine
  yazildiginda kaybolmaz. Log: %AppData%\PkfRobot\logs\ajan-<tarih>.log
  Sunucu adresleri appsettings.json > Ajan bolumunde.

GRID DOGRULAMA (varsayilan KAPALI):
  Acikken GridDoldur bittikten sonra ekran goruntusundeki Karsi Hesap Kodu
  kolonu okunur ve yazilmasi beklenen listeyle satir satir karsilastirilir.
  Uyusmazlik UYARI olarak log'a yazilir, robot DURDURULMAZ - Kaydet'e zaten
  kullanici basiyor, bu bir on eleme. Kapaliyken hicbir API cagrisi yapilmaz.
  Anahtar: --anthropic-anahtari-ayarla. Acmak: GoruntuDogrulama.Aktif = true.
  DIKKAT: acikken ekran goruntusu Anthropic API'sine gonderilir.

NOTLAR:
  * Varsayilan olarak DryRun aciktir, Kaydet adimlari atlanir.
  * Her calistirma C:\RobotLog altinda ayri klasor acar (log + ekran goruntusu).
  * Bir sey ters giderse once --probe calistir.
  * Ekran KILITLI iken UI Automation calismaz. Oturum acik olmali.
");
    }
}

public class Parametreler
{
    public string? GorevYolu { get; set; }
    public string? ConfigYolu { get; set; }
    public string? FirmaKodu { get; set; }
    public string? Sifre { get; set; }
    public bool Probe { get; set; }
    public bool Kalibre { get; set; }
    public bool Ajan { get; set; }
    public bool AnahtariSifirla { get; set; }

    /// <summary>Arguman verilmedi: masaustu arayuzu acilir.</summary>
    public bool Arayuz { get; set; }
    public bool CanliMod { get; set; }
    public bool Yardim { get; set; }

    /// <summary>Goruntu dogrulamasinin API anahtarini sorup DPAPI ile kaydeder, sonra cikar.</summary>
    public bool AnthropicAnahtariAyarla { get; set; }

    public Dictionary<string, string> EkDegiskenler { get; } = new();

    public static Parametreler Coz(string[] args)
    {
        var p = new Parametreler();

        // Argumansiz calistirma artik yardim degil ARAYUZ aciyor: kullanicinin
        // exe'ye cift tiklamasi en dogal davranis ve karsiliginda bir yardim
        // metni gormesi anlamsizdi. Yardim icin --yardim duruyor.
        if (args.Length == 0) { p.Arayuz = true; return p; }

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            string? Sonraki() => i + 1 < args.Length ? args[++i] : null;

            switch (a)
            {
                case "--gorev":     p.GorevYolu = Sonraki(); break;
                case "--config":    p.ConfigYolu = Sonraki(); break;
                case "--firma":     p.FirmaKodu = Sonraki(); break;
                case "--sifre":     p.Sifre = Sonraki(); break;
                case "--probe":     p.Probe = true; break;
                case "--kalibre":   p.Kalibre = true; break;
                case "--ajan":      p.Ajan = true; break;
                case "--anahtari-sifirla":
                    p.AnahtariSifirla = true;
                    p.Ajan = true;   // tek basina anlami yok; ajan modunu kendisi acar
                    break;
                case "--anthropic-anahtari-ayarla":
                    p.AnthropicAnahtariAyarla = true;
                    break;
                case "--canli":     p.CanliMod = true; break;
                case "--yardim":
                case "--help":
                case "-h":          p.Yardim = true; break;
                case "--degisken":
                    var d = Sonraki();
                    if (d != null && d.Contains('='))
                    {
                        var idx = d.IndexOf('=');
                        p.EkDegiskenler[d[..idx]] = d[(idx + 1)..];
                    }
                    break;
            }
        }
        return p;
    }
}
