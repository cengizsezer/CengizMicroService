using System.Diagnostics;
using System.Runtime.Versioning;
using PkfRobot.Ayarlar;

namespace PkfRobot.Arayuz;

/// <summary>Ekrandaki ORKA penceresinin o anki hali.</summary>
/// <param name="Tutamac">ANA pencerenin tutamaci; bulunamadiysa <see cref="IntPtr.Zero"/>.</param>
/// <param name="Olcu">Ana pencerenin ekrandaki dikdortgeni.</param>
/// <param name="Baslik">Ana pencerenin basligi; hangi pencerenin olculdugu log'da gorunsun.</param>
/// <param name="TamEkran">Maximize durumda mi?</param>
/// <param name="SimgeDurumunda">Gorev cubuguna indirilmis mi?</param>
/// <param name="Surecler">ORKA'ya ait surec kimlikleri.</param>
public record OrkaPencereDurumu(
    IntPtr Tutamac,
    PencereOlcusu Olcu,
    string Baslik,
    bool TamEkran,
    bool SimgeDurumunda,
    IReadOnlyCollection<int> Surecler)
{
    public bool Bulundu => Tutamac != IntPtr.Zero;

    public static OrkaPencereDurumu Yok(IReadOnlyCollection<int>? surecler = null)
        => new(IntPtr.Zero, default, string.Empty, false, false, surecler ?? Array.Empty<int>());
}

/// <summary>Bir ekran noktasindaki ust seviye pencerenin kimligi.</summary>
/// <param name="Surec">Pencerenin surec kimligi; okunamadiysa null.</param>
/// <param name="Baslik">Pencere basligi; okunamadiysa bos.</param>
/// <param name="SurecAdi">Surecin adi (mspaint, OrkaWinIceberg.64...); okunamadiysa bos.</param>
public record TiklananPencere(int? Surec, string Baslik, string SurecAdi)
{
    public static readonly TiklananPencere Okunamadi = new(null, string.Empty, string.Empty);
}

/// <summary>
/// Masaustundeki bir ust seviye pencerenin HAM hali: hicbir filtre uygulanmamis.
/// Tarama tek noktadan (<see cref="OrkaPenceresi.TumUstSeviyePencereler"/>) gelir;
/// hangi pencerenin elenecegine cagiran karar verir.
/// </summary>
/// <param name="Tutamac">Pencerenin Win32 tutamaci.</param>
/// <param name="Pid">Pencereyi acan surec; okunamadiysa 0.</param>
/// <param name="Baslik">GetWindowText ciktisi; basliksiz pencerede bos.</param>
/// <param name="Olcu">Ekrandaki dikdortgen (<see cref="OrkaPenceresi.OlcuAl"/>).</param>
/// <param name="Gorunur">IsWindowVisible.</param>
/// <param name="Sahip">GW_OWNER. Bilgi olarak tasiniyor, ELEME OLCUTU DEGIL.</param>
public sealed record UstSeviyePencere(
    IntPtr Tutamac,
    int Pid,
    string Baslik,
    PencereOlcusu Olcu,
    bool Gorunur,
    IntPtr Sahip)
{
    /// <summary>Baslik verilen parcayi iceriyor mu (buyuk/kucuk harf duyarsiz)?</summary>
    public bool BasligaUyuyor(string? parca)
        => !string.IsNullOrEmpty(Baslik) &&
           !string.IsNullOrWhiteSpace(parca) &&
           Baslik.Contains(parca, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Bir pencereyi one getirme denemesinin sonucu.
///
/// <b>Neden bool yetmiyor:</b> iki ayri soru var ve ikisi de log'a dusmeli.
/// <c>SetForegroundWindow</c> "istegi kabul ettim" der (foreground lock kurallarina
/// takilirsa false doner), pencerenin GERCEKTEN one gelmesi ayri bir olay ve
/// asenkron. Istek reddedilip pencere yine de one gelebilir; kabul edilip
/// gelmeyebilir de. Ikisini tek bool'a sikistirmak, kaybolan tusun sebebini
/// gormeyi imkansiz kilardi.
/// </summary>
/// <param name="CagriKabulEdildi">SetForegroundWindow true dondu mu.</param>
/// <param name="OndeMi">Pencere sure dolmadan gercekten on plana geldi mi.</param>
/// <param name="ZatenOndeydi">Cagriya hic gerek kalmadi mi.</param>
/// <param name="GecenMs">Dogrulamanin surdugu sure.</param>
public readonly record struct OneGetirmeRaporu(
    bool CagriKabulEdildi,
    bool OndeMi,
    bool ZatenOndeydi,
    int GecenMs)
{
    /// <summary>Tutamac yok: hicbir cagri yapilmadi.</summary>
    public static readonly OneGetirmeRaporu Gecersiz = new(false, false, false, 0);
}

/// <summary>
/// ORKA penceresini surec adindan bulur, olcusunu okur, one getirir.
///
/// <b>Neden baslik degil surec:</b> ORKA'nin pencere basligi surume ve acik
/// firmaya gore degisiyor (<c>ORKA_0001_2026</c>); surec adi sabit. Ajan
/// tarafinda ORKA'nin acik olup olmadigi da ayni sebeple surece bakilarak
/// belirleniyor (bkz. <see cref="PkfRobot.Ajan.OrkaSureci"/>).
///
/// <b>Baslik yine de gerekli:</b> surec ORKA'nin butun pencerelerini kapsiyor
/// -- ana pencere, modal diyaloglar, firma sifresi popup'i. Oran ise <b>yalniz
/// ana pencereye</b> goreli anlamli, cunku <c>AdimMotoru.Tikla</c> hedef
/// pencereyi config'deki <c>Pencereler.AnaEkran</c> basligindan buluyor. Bu
/// yuzden ana pencere ayni baslikla secilir; baslik verilmezse
/// <see cref="Process.MainWindowHandle"/>'a dusulur.
/// </summary>
[SupportedOSPlatform("windows")]
public class OrkaPenceresi
{
    private readonly string _surecAdi;
    private readonly string[] _anaBaslikAdaylari;

    /// <summary>Config'den gelen ham baslik; teshis dokumunda oldugu gibi gosterilir.</summary>
    private readonly string _anaBaslikHam;

    /// <param name="surecAdi">ORKA exe'sinin uzantisiz adi (<c>Ajan.OrkaSurecAdi</c>).</param>
    /// <param name="anaPencereBasligi">
    /// Ana pencere basligindan bir parca (<c>Pencereler.AnaEkran</c>); '|' ile
    /// birden fazla aday verilebilir. Bos birakilirsa ya da hicbir aday tutmazsa
    /// ORKA'nin olculebilir EN BUYUK penceresi secilir.
    /// </param>
    public OrkaPenceresi(string surecAdi, string? anaPencereBasligi = null)
    {
        _surecAdi = surecAdi;
        _anaBaslikHam = anaPencereBasligi ?? string.Empty;
        _anaBaslikAdaylari = (anaPencereBasligi ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>ORKA'nin su anki hali. ORKA kapaliysa <see cref="OrkaPencereDurumu.Bulundu"/> false.</summary>
    public OrkaPencereDurumu Durum()
    {
        var surecler = Surecler(out var yedekTutamac);
        if (surecler is null) return OrkaPencereDurumu.Yok();

        var tutamac = AnaPencereBul(surecler, yedekTutamac);

        if (tutamac == IntPtr.Zero || !Win32.IsWindow(tutamac))
            return OrkaPencereDurumu.Yok(surecler);

        return new OrkaPencereDurumu(
            tutamac,
            OlcuAl(tutamac),
            Win32.Baslik(tutamac),
            Win32.IsZoomed(tutamac),
            Win32.IsIconic(tutamac),
            surecler);
    }

    /// <summary>
    /// ORKA sureclerinin kimlikleri; <paramref name="yedekTutamac"/> isletim
    /// sisteminin bildirdigi ilk ana pencere. Surec listesi hic alinamadiysa
    /// null doner.
    ///
    /// <c>Durum()</c> ile teshis dokumu ayni listeyi gormeli; iki yerde ayri
    /// toplanirsa dokum "bakarken baska" olurdu.
    /// </summary>
    private List<int>? Surecler(out IntPtr yedekTutamac)
    {
        var surecler = new List<int>();
        yedekTutamac = IntPtr.Zero;

        Process[] bulunanlar;
        try
        {
            bulunanlar = Process.GetProcessesByName(_surecAdi);
        }
        catch (Exception)
        {
            return null;
        }

        try
        {
            foreach (var surec in bulunanlar)
            {
                surecler.Add(surec.Id);

                // Ana penceresi olmayan surec de ORKA sayiliyor (giris ekrani
                // henuz acilmamis olabilir): surec listesi tiklama denetimi icin
                // gerekli, tutamac ise yalniz olcu icin.
                try
                {
                    if (yedekTutamac == IntPtr.Zero && surec.MainWindowHandle != IntPtr.Zero)
                        yedekTutamac = surec.MainWindowHandle;
                }
                catch (Exception)
                {
                    // Surec bu arada kapanmis olabilir; liste yeterli.
                }
            }
        }
        finally
        {
            foreach (var s in bulunanlar) s.Dispose();
        }

        return surecler;
    }

    /// <summary>
    /// ORKA'nin ANA penceresi.
    ///
    /// <b>Neden <see cref="Process.MainWindowHandle"/> yetmiyor:</b> o, surece
    /// ait ilk gorunur ve SAHIPSIZ pencereyi dondurur. ORKA'da bu pencere ana
    /// form degil: ana form gizli bir kabuk pencere tarafindan sahipleniliyor,
    /// tek sahipsiz pencere ise 0x0 olculu bir kabuk. Yedege dusuldugunde oranin
    /// paydasi sifir kaliyor.
    ///
    /// <b>Neden GW_OWNER filtresi yok:</b> once "sahipli pencere = modal diyalog"
    /// varsayilip elenmisti; teshis dokumu bunun ORKA'da tersine dondugunu
    /// gosterdi -- filtre asil ana pencereyi eliyordu. Sahiplik yerine
    /// <b>olculebilirlik</b> ve <b>baslik</b> eliyor: gorunur, alani sifirdan
    /// buyuk ve basligi <c>Pencereler.AnaEkran</c> adaylarindan birini iceren
    /// pencere. Birden fazlasi uyarsa EN BUYUK ALANLI secilir; ana pencere her
    /// zaman kendi diyaloglarindan buyuk.
    /// </summary>
    private IntPtr AnaPencereBul(IReadOnlyCollection<int> surecler, IntPtr yedek)
    {
        if (surecler.Count == 0) return IntPtr.Zero;

        var adaylar = UstSeviyePencereler(surecler);
        var basligaUyan = adaylar.Where(BasligaUyuyor).ToList();

        // Baslik tutmadiysa sart DUSURULUYOR: ORKA giris ekraninda olabilir,
        // baslik surumle degismis olabilir ya da AnaEkran hic tanimlanmamis
        // olabilir. Olculebilir bir ORKA penceresi varsa onu kullanmak hicbir
        // sey kullanmamaktan iyi.
        var secilen = EnBuyuk(basligaUyan) ?? EnBuyuk(adaylar);
        if (secilen is not null) return secilen.Tutamac;

        // Hicbir olculebilir aday yok. Yedege ANCAK kendisi olculebiliyorsa
        // dusulur: 0x0 bir kabuk pencereyi dondurmek "bulundu" deyip orani
        // sessizce bozmak olurdu -- asil hata buydu.
        return OlcuAl(yedek).Gecerli ? yedek : IntPtr.Zero;
    }

    /// <summary>Basligi <c>Pencereler.AnaEkran</c> adaylarindan birini iceriyor mu?</summary>
    private bool BasligaUyuyor(PencereAdayi aday)
        => _anaBaslikAdaylari.Any(p => aday.Baslik.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Alani en buyuk aday; liste bossa null.</summary>
    private static PencereAdayi? EnBuyuk(IReadOnlyCollection<PencereAdayi> adaylar)
        => adaylar.Count == 0
            ? null
            : adaylar.MaxBy(a => (long)a.Olcu.Genislik * a.Olcu.Yukseklik);

    /// <summary>Ana pencere adayi: ORKA surecine ait, gorunur, olculebilir bir pencere.</summary>
    private sealed record PencereAdayi(IntPtr Tutamac, string Baslik, PencereOlcusu Olcu);

    /// <summary>
    /// Masaustundeki <b>butun</b> ust seviye pencereler -- projedeki TEK pencere
    /// tarama noktasi. Filtre yok: gorunmeyen, basliksiz, olculemeyen ve sahipli
    /// pencereler de listede. Eleme kararini cagiran verir.
    ///
    /// <b>Neden EnumWindows, neden UIA degil:</b> ORKA'nin pencereleri gizli bir
    /// kabuk pencere tarafindan SAHIPLENILIYOR (owner != 0) ve UIA bunlari
    /// <c>GetDesktop().FindAllChildren()</c> altinda listelemiyor. Uctan uca
    /// denemede "Firma Sifresini Giriniz." popup'i ekranda dururken 40 sn
    /// timeout'a dusmesinin ve hata mesajindaki "Ekrandakiler" listesinde hic
    /// gorunmemesinin sebebi buydu. EnumWindows sahiplige bakmadan hepsini
    /// donduruyor.
    /// </summary>
    public static List<UstSeviyePencere> TumUstSeviyePencereler()
    {
        var sonuc = new List<UstSeviyePencere>();

        try
        {
            Win32.EnumWindows((tutamac, _) =>
            {
                Win32.GetWindowThreadProcessId(tutamac, out var pid);

                sonuc.Add(new UstSeviyePencere(
                    tutamac,
                    (int)pid,
                    Win32.Baslik(tutamac),
                    OlcuAl(tutamac),
                    Win32.IsWindowVisible(tutamac),
                    Win32.GetWindow(tutamac, Win32.GW_OWNER)));

                return true;
            }, IntPtr.Zero);
        }
        catch (Exception)
        {
            // Pencere listesi alinamadi; cagiran yedege duser.
        }

        return sonuc;
    }

    /// <summary>
    /// ORKA sureclerine ait <b>gorunur ve olculebilir</b> ust seviye pencereler.
    /// Sahiplige BAKILMIYOR (bkz. <see cref="AnaPencereBul"/>); eleme olcuye
    /// gore: genislik ya da yukseklik sifirsa o pencere oranin paydasi olamaz.
    /// </summary>
    private static List<PencereAdayi> UstSeviyePencereler(IReadOnlyCollection<int> surecler)
        => TumUstSeviyePencereler()
            .Where(p => p.Pid != 0 && surecler.Contains(p.Pid) && p.Gorunur && p.Olcu.Gecerli)
            .Select(p => new PencereAdayi(p.Tutamac, p.Baslik, p.Olcu))
            .ToList();

    // ================== GECICI TESHIS ==================
    // Buradan asagisi "AnaPencereBul neden bos donuyor?" sorusunu gormek icin
    // eklendi. Karar mantigina DOKUNMUYOR: kendi ham taramasini yapiyor, filtre
    // sirasini ve sonucunu degistirmiyor. Sebep anlasilinca bu blok ve
    // KalibrasyonPaneli'ndeki cagrilari silinecek.

    /// <summary>Teshis dokumundeki tek bir pencere satiri; hicbir filtre uygulanmamis hali.</summary>
    public sealed record TeshisPenceresi(
        IntPtr Tutamac,
        int Pid,
        bool Gorunur,
        IntPtr Sahip,
        PencereOlcusu Olcu,
        string Baslik);

    /// <summary>
    /// ORKA surecine ait <b>butun</b> ust seviye pencereler ve
    /// <see cref="AnaPencereBul"/>'un hangi asamada kac pencere eledigi.
    /// </summary>
    public string Teshis()
    {
        var yazi = new System.Text.StringBuilder();
        yazi.AppendLine("=== GECICI PENCERE TESHISI (OrkaPenceresi.AnaPencereBul) ===");
        yazi.AppendLine($"Zaman: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        yazi.AppendLine($"Beklenen surec adi: \"{_surecAdi}\"");
        yazi.AppendLine($"cfg.Pencereler.AnaEkran: \"{_anaBaslikHam}\"");
        yazi.AppendLine($"  -> aranan baslik adaylari ({_anaBaslikAdaylari.Length}): " +
                        (_anaBaslikAdaylari.Length == 0
                            ? "YOK (baslik bos; dogrudan MainWindowHandle yedegine dusulur)"
                            : string.Join(", ", _anaBaslikAdaylari.Select(a => $"\"{a}\""))));

        var surecler = Surecler(out var yedek);
        if (surecler is null)
        {
            yazi.AppendLine("Surec listesi ALINAMADI (Process.GetProcessesByName patladi).");
            return yazi.ToString();
        }

        yazi.AppendLine($"ORKA surecleri ({surecler.Count}): " +
                        (surecler.Count == 0 ? "YOK" : string.Join(", ", surecler)));

        var ham = HamUstSeviyePencereler(surecler, out var toplamUstSeviye);

        // AnaPencereBul'un GERCEK sonucu: burada yeniden hesaplanmiyor, ayni
        // metot cagriliyor. Aksi halde dokum "bakarken baska" olurdu.
        var secilen = AnaPencereBul(surecler, yedek);

        yazi.AppendLine($"EnumWindows'tan gelen toplam ust seviye pencere: {toplamUstSeviye}");
        yazi.AppendLine($"Bunlardan ORKA surecine ait: {ham.Count}");
        yazi.AppendLine();

        yazi.AppendLine("--- ORKA surecine ait TUM ust seviye pencereler (filtresiz ham liste) ---");
        yazi.AppendLine("hwnd       | pid   | gorunur | GW_OWNER   | rect (S,U,G,Y)          | baslik");

        if (ham.Count == 0)
            yazi.AppendLine("(bos -- EnumWindows bu surece ait hic ust seviye pencere dondurmedi)");

        foreach (var p in ham)
        {
            yazi.AppendLine(
                $"0x{p.Tutamac.ToInt64():X8} | {p.Pid,-5} | {(p.Gorunur ? "evet" : "HAYIR"),-7} | " +
                $"0x{p.Sahip.ToInt64():X8} | " +
                $"({p.Olcu.Sol}, {p.Olcu.Ust}, {p.Olcu.Genislik}, {p.Olcu.Yukseklik})".PadRight(23) + " | " +
                (string.IsNullOrEmpty(p.Baslik) ? "(basliksiz)" : p.Baslik));
        }

        // Sayimlar ORKA'ya ait ham kume uzerinde asama asama yapiliyor. Canli
        // kodda eleme sirasi pid -> gorunur -> olcu; buradaki siralama sonuc
        // kumesini degistirmiyor, yalnizca "kac tanesi nerede dustu"yu ORKA
        // penceresi bazinda okunur kiliyor. GW_OWNER sutunu bilgi olarak duruyor
        // ama ARTIK ELEMIYOR: ana pencere sahipli cikti.
        var gorunurElenen = ham.Count(p => !p.Gorunur);
        var olcuElenen = ham.Count(p => p.Gorunur && !p.Olcu.Gecerli);
        var adaylar = ham.Where(p => p.Gorunur && p.Olcu.Gecerli).ToList();
        var eslesenler = adaylar
            .Where(p => _anaBaslikAdaylari.Any(a => p.Baslik.Contains(a, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        yazi.AppendLine();
        yazi.AppendLine("--- Filtre dokumu ---");
        yazi.AppendLine($"pid filtresi ile elenen (baska surec) : {toplamUstSeviye - ham.Count}");
        yazi.AppendLine($"IsWindowVisible = false ile elenen    : {gorunurElenen}");
        yazi.AppendLine($"olcusu sifir (G ya da Y = 0) ile elenen: {olcuElenen}");
        yazi.AppendLine($"GW_OWNER (sahiplik)                   : artik ELEMIYOR");
        yazi.AppendLine($"baslik eslesmedigi icin elenen        : {adaylar.Count - eslesenler.Count}");
        yazi.AppendLine($"basliga uyan aday sayisi              : {eslesenler.Count}");
        yazi.AppendLine();

        yazi.AppendLine($"Secilen ana pencere      : 0x{secilen.ToInt64():X8}" +
                        (secilen == IntPtr.Zero ? "  (BULUNAMADI)" : $"  baslik: {Win32.Baslik(secilen)}"));
        yazi.AppendLine($"MainWindowHandle yedegi  : 0x{yedek.ToInt64():X8}" +
                        (yedek == IntPtr.Zero ? "  (yok)" : string.Empty));
        yazi.AppendLine($"Yedege dusuldu mu        : {YedekNotu(adaylar.Count, eslesenler.Count, secilen)}");

        if (secilen != IntPtr.Zero)
        {
            var olcu = OlcuAl(secilen);
            yazi.AppendLine($"Secilen pencerenin olcusu: ({olcu.Sol}, {olcu.Ust}, {olcu.Genislik}, {olcu.Yukseklik})" +
                            (olcu.Gecerli ? string.Empty : "  <-- GECERSIZ (G ya da Y sifir)"));
            yazi.AppendLine($"IsWindow / IsIconic / IsZoomed: {Win32.IsWindow(secilen)} / " +
                            $"{Win32.IsIconic(secilen)} / {Win32.IsZoomed(secilen)}");
        }

        return yazi.ToString();
    }

    /// <summary>Teshis dokumundeki "yedege dusuldu mu" satirinin metni.</summary>
    private static string YedekNotu(int adaySayisi, int eslesenSayisi, IntPtr secilen)
    {
        if (adaySayisi > 0)
            return eslesenSayisi > 0
                ? "hayir (baslik esletti)"
                : "hayir (baslik tutmadi -> en buyuk olculebilir pencere secildi)";

        return secilen == IntPtr.Zero
            ? "EVET ama yedek de olculemedi -> BULUNAMADI"
            : "EVET (olculebilir aday yok, yedegin olcusu gecerli)";
    }

    /// <summary>
    /// Teshis dokumunu dosyaya yazar ve yolu doner; yazilamazsa null.
    /// Basliklar uzun, mesaj kutusuna sigmiyor -- asil kopya dosyada.
    /// </summary>
    public static string? TeshisKaydet(string? klasor, string dokum)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(klasor)) return null;

            Directory.CreateDirectory(klasor);
            var dosya = Path.Combine(klasor, $"pencere-teshis_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
            File.WriteAllText(dosya, dokum, System.Text.Encoding.UTF8);
            return dosya;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// ORKA surecine ait ust seviye pencerelerin <b>filtresiz</b> listesi:
    /// gorunur olmayanlar ve sahipli olanlar da icinde. Yalniz teshis icin --
    /// <see cref="UstSeviyePencereler"/> karar yolunda kullanilmaya devam ediyor.
    /// </summary>
    private static List<TeshisPenceresi> HamUstSeviyePencereler(
        IReadOnlyCollection<int> surecler, out int toplamUstSeviye)
    {
        var hepsi = TumUstSeviyePencereler();
        toplamUstSeviye = hepsi.Count;

        return hepsi
            .Where(p => p.Pid != 0 && surecler.Contains(p.Pid))
            .Select(p => new TeshisPenceresi(p.Tutamac, p.Pid, p.Gorunur, p.Sahip, p.Olcu, p.Baslik))
            .ToList();
    }

    // ================ GECICI TESHIS SONU ================

    /// <summary>
    /// Bir ust seviye pencerenin altindaki TEK bir kontrol: sinif adi ekranin
    /// hangi bolumunun ayakta oldugunu soyluyor.
    /// </summary>
    /// <param name="Tutamac">Kontrolun tutamaci.</param>
    /// <param name="SinifAdi">Delphi/VCL sinif adi ("TdxRibbonTab", "TPanel"...).</param>
    /// <param name="Baslik">Kontrolun metni; cogu kontrolde bos.</param>
    /// <param name="Gorunur">WS_VISIBLE. Kapali sekmelerin kontrolleri agacta KALIR,
    /// yalnizca gorunurlugu duser -- "sekme acik mi" sorusu buradan cevaplaniyor.</param>
    /// <param name="Derinlik">Kok pencereden kac seviye asagida.</param>
    public sealed record AltPencere(
        IntPtr Tutamac,
        string SinifAdi,
        string Baslik,
        bool Gorunur,
        int Derinlik);

    /// <summary>
    /// Verilen pencerenin TUM alt kontrolleri (torunlar dahil).
    ///
    /// <b>Neden gerekli:</b> ORKA'da modul/sekme gecisleri ust seviye pencere
    /// ACMIYOR -- baslik da degismiyor. Baslik bakan <c>Dogrula</c> bu yuzden
    /// "Veri Transferi acildi mi" sorusunu cevaplayamiyor ve robot yanlis
    /// ekranda devam edebiliyor. Alt pencere agaci UIA'ya kapali ORKA'da
    /// okunabilen tek yapisal sinyal.
    ///
    /// Ekrana dokunmuyor, yalniz okuyor. Derinlik <see cref="Win32.GetParent"/>
    /// zinciriyle hesaplaniyor; zincir kok'e varmadan biterse sayac
    /// <paramref name="maxDerinlik"/>'te durur (bozuk agacta sonsuz dongu olmasin).
    /// </summary>
    public static List<AltPencere> AltPencereler(IntPtr kok, int maxDerinlik = 12)
    {
        var sonuc = new List<AltPencere>();
        if (kok == IntPtr.Zero) return sonuc;

        Win32.EnumChildWindows(kok, (h, _) =>
        {
            sonuc.Add(new AltPencere(
                h,
                Win32.SinifAdi(h),
                Win32.Baslik(h),
                Win32.IsWindowVisible(h),
                Derinlik(h, kok, maxDerinlik)));
            return true;
        }, IntPtr.Zero);

        return sonuc;
    }

    private static int Derinlik(IntPtr cocuk, IntPtr kok, int maxDerinlik)
    {
        var seviye = 0;
        var gezen = cocuk;

        while (gezen != IntPtr.Zero && gezen != kok && seviye < maxDerinlik)
        {
            gezen = Win32.GetParent(gezen);
            seviye++;
        }

        return seviye;
    }

    /// <summary>On planda olan pencerenin tutamaci.</summary>
    public static IntPtr OnPencere() => Win32.GetForegroundWindow();

    /// <summary>Tutamac hala yasayan bir pencereyi mi gosteriyor?</summary>
    public static bool PencereMi(IntPtr tutamac)
        => tutamac != IntPtr.Zero && Win32.IsWindow(tutamac);

    /// <summary>
    /// Pencere girdi kabul ediyor mu? Uzerinde modal diyalog acikken false doner --
    /// odak duzeltmenin "dokunma" karari buna bakiyor.
    /// </summary>
    public static bool EtkinMi(IntPtr tutamac)
        => tutamac != IntPtr.Zero && Win32.IsWindowEnabled(tutamac);

    /// <summary>Pencerenin basligi; okunamazsa bos dizge.</summary>
    public static string PencereBasligi(IntPtr tutamac) => Win32.Baslik(tutamac);

    /// <summary>Pencereyi acan surecin kimligi; okunamazsa 0.</summary>
    public static int PencereSureci(IntPtr tutamac)
    {
        if (tutamac == IntPtr.Zero) return 0;

        Win32.GetWindowThreadProcessId(tutamac, out var pid);
        return (int)pid;
    }

    /// <summary>Pencerenin mesaj dongusu verilen sure icinde cevap veriyor mu?</summary>
    public static bool YanitVeriyorMu(IntPtr tutamac, int zamanAsimiMs)
        => Win32.YanitVeriyorMu(tutamac, (uint)Math.Max(0, zamanAsimiMs));

    /// <summary>Dogrulama poll'unun toplam suresi ve arasi.</summary>
    public const int OneGetirmeZamanAsimiMs = 1500;
    public const int OneGetirmeAralikMs = 25;

    /// <summary>
    /// Pencereyi one getirir, ONE GELDIGINI DOGRULAR; simge durumundaysa once geri acar.
    ///
    /// <b>Neden dogrulama sart:</b> onceki surum pencereleri FlaUI'nin
    /// <c>AutomationElement.Focus()/SetForeground()</c> ciftiyle one getiriyordu ve
    /// FlaUI her cagrinin ardindan <c>Wait.UntilResponsive</c> calistiriyor --
    /// yani <c>SendMessageTimeout(WM_NULL)</c> ile pencerenin mesaj kuyrugunun
    /// aktivasyonu ISLEDIGINI bekliyordu. Ciplak <c>SetForegroundWindow</c>'a
    /// gecerken o bariyer dustu: cagri asenkron oldugu icin bir sonraki adimin ilk
    /// tusu pencere daha one gelmeden gidiyor ve YUTULUYORDU (modul ekraninda
    /// "SAG x3" yerine iki adim ilerlemesinin sebebi buydu).
    ///
    /// Burada durdurma yok: sure dolsa da rapor dondurulur, karari ve log'u cagiran verir.
    /// </summary>
    public static OneGetirmeRaporu OneGetir(IntPtr tutamac)
    {
        if (tutamac == IntPtr.Zero) return OneGetirmeRaporu.Gecersiz;

        if (Win32.IsIconic(tutamac)) Win32.ShowWindow(tutamac, Win32.SW_RESTORE);

        var saat = Stopwatch.StartNew();

        return OneGetirCekirdek(
            tutamac,
            () => Win32.SetForegroundWindow(tutamac),
            Win32.GetForegroundWindow,
            h => FlaUI.Core.Input.Wait.UntilResponsive(h),
            Thread.Sleep,
            () => saat.ElapsedMilliseconds);
    }

    /// <summary>
    /// One getirmenin EKRANA DOKUNMAYAN cekirdegi: butun Windows cagrilari disaridan
    /// veriliyor, boylece "kac kere yokladi, ne zaman pes etti, hangi durumu
    /// raporladi" sorulari ORKA olmadan test edilebiliyor.
    /// </summary>
    /// <param name="oneGetir">SetForegroundWindow; dondurdugu deger rapora gecer.</param>
    /// <param name="onPencere">GetForegroundWindow.</param>
    /// <param name="yanitBekle">Pencere one geldikten SONRA mesaj kuyrugunu bekleyen bariyer.</param>
    /// <param name="uyu">Yoklamalar arasi bekleme.</param>
    /// <param name="saatMs">Gecen sure kaynagi.</param>
    public static OneGetirmeRaporu OneGetirCekirdek(
        IntPtr hedef,
        Func<bool> oneGetir,
        Func<IntPtr> onPencere,
        Action<IntPtr> yanitBekle,
        Action<int> uyu,
        Func<long> saatMs,
        int zamanAsimiMs = OneGetirmeZamanAsimiMs,
        int aralikMs = OneGetirmeAralikMs)
    {
        if (hedef == IntPtr.Zero) return OneGetirmeRaporu.Gecersiz;

        var basladi = saatMs();

        // Zaten ondeyse cagri hic yapilmiyor: gereksiz aktivasyon ekrani titretiyor
        // ve baska bir pencerenin modalini one cekebiliyor. Bariyer yine de
        // calisiyor -- "onde" olmak "mesajlarini isledi" demek degil.
        if (onPencere() == hedef)
        {
            yanitBekle(hedef);
            return new OneGetirmeRaporu(true, true, true, (int)(saatMs() - basladi));
        }

        var kabul = oneGetir();

        while (true)
        {
            if (onPencere() == hedef)
            {
                yanitBekle(hedef);
                return new OneGetirmeRaporu(kabul, true, false, (int)(saatMs() - basladi));
            }

            if (saatMs() - basladi >= zamanAsimiMs)
                return new OneGetirmeRaporu(kabul, false, false, (int)(saatMs() - basladi));

            uyu(aralikMs);
        }
    }

    /// <summary>
    /// Bir pencerenin ekrandaki dikdortgeni -- <b>tek olcu kaynagi</b>.
    /// Oranin paydasi nerede hesaplaniyorsa (adim motoru, kalibre modu,
    /// kalibrasyon paneli, teshis dokumu) olcuyu buradan alir. Iki ayri
    /// kaynak -- Win32 GetWindowRect ile UIA BoundingRectangle -- ayni
    /// pencere icin farkli dikdortgen dondurebiliyor; ikisi karisirsa
    /// robotun tikladigi yer ile kullanicinin olctugu yer sessizce ayrisir.
    ///
    /// Okunamazsa <c>default</c> doner (G=Y=0, yani <c>Gecerli=false</c>).
    /// Sol/Ust NEGATIF olabilir -- ikinci monitor ve DWM kenarlik payi;
    /// deger kirpilmaz, isaretli aritmetikle tasinir.
    /// </summary>
    public static PencereOlcusu OlcuAl(IntPtr hwnd)
    {
        if (!Win32.GetWindowRect(hwnd, out var r)) return default;
        return new PencereOlcusu(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>Verilen ekran noktasindaki pencerenin surec kimligi; okunamazsa null.</summary>
    public static int? NoktadakiSurec(int x, int y) => NoktadakiPencere(x, y).Surec;

    /// <summary>
    /// Verilen ekran noktasindaki ust seviye pencerenin kimligi: surec, baslik,
    /// surec adi. Ret mesajinin "hangi pencereye tikladiniz" satiri buradan.
    /// </summary>
    public static TiklananPencere NoktadakiPencere(int x, int y)
    {
        var nokta = new Win32.POINT { X = x, Y = y };
        var pencere = Win32.WindowFromPoint(nokta);
        if (pencere == IntPtr.Zero) return TiklananPencere.Okunamadi;

        // Tiklanan sey bir alt kontrol olabilir; surec sorusu ust pencereden
        // sorulmali. Ust pencere ORKA'nin bir diyalogu da olabilir -- bu
        // istenen durum, denetim surece bakiyor.
        var kok = Win32.GetAncestor(pencere, Win32.GA_ROOT);
        if (kok != IntPtr.Zero) pencere = kok;

        Win32.GetWindowThreadProcessId(pencere, out var pid);
        if (pid == 0) return TiklananPencere.Okunamadi;

        return new TiklananPencere((int)pid, Win32.Baslik(pencere), SurecAdi((int)pid));
    }

    /// <summary>Surec adi; surec bu arada kapandiysa bos dizge.</summary>
    private static string SurecAdi(int pid)
    {
        try
        {
            using var surec = Process.GetProcessById(pid);
            return surec.ProcessName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>Tiklama anini <see cref="KoordinatSecimi"/>'nin bekledigi bicime cevirir.</summary>
    public TiklamaOrtami Ortam(int mutlakX, int mutlakY)
    {
        var durum = Durum();
        var tiklanan = NoktadakiPencere(mutlakX, mutlakY);

        return new TiklamaOrtami(
            mutlakX,
            mutlakY,
            durum.Bulundu ? durum.Olcu : null,
            tiklanan.Surec,
            durum.Surecler,
            durum.TamEkran,
            tiklanan.Baslik,
            tiklanan.SurecAdi,
            _surecAdi,
            durum.Baslik);
    }
}
