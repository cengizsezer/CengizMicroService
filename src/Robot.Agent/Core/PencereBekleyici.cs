using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using PkfRobot.Arayuz;
using PkfRobot.Config;

namespace PkfRobot.Core;

/// <summary>
/// Thread.Sleep ile ilerleyen robot ilk yavas gunde coker.
/// Bu sinif "beklenen pencere gelene kadar bekle" mantigini saglar.
/// ORKA'nin ic kontrolleri UIA'ya kapali ama PENCERE BASLIKLARI okunabiliyor,
/// dogrulamanin tamami bunun uzerine kurulu.
///
/// <b>Tarama neden EnumWindows:</b> onceki surum pencereleri UIA'nin
/// <c>GetDesktop().FindAllChildren()</c> cagrisiyla listeliyordu. ORKA'nin
/// pencereleri gizli bir kabuk pencere tarafindan SAHIPLENILIYOR (owner != 0)
/// ve UIA bunlari masaustunun cocugu olarak vermiyor: uctan uca denemede
/// "Firma Sifresini Giriniz." popup'i ekranda dururken 40 sn timeout'a dustu ve
/// hata mesajindaki "Ekrandakiler (14)" listesinde hic gorunmedi. Tarama artik
/// <see cref="OrkaPenceresi.TumUstSeviyePencereler"/> uzerinden -- ana pencere
/// bulma (<c>OrkaPenceresi.AnaPencereBul</c>) ile ayni kaynak, ayni EnumWindows.
///
/// Eleme yalnizca <b>gorunurluk</b> ve <b>baslik</b> ile yapiliyor:
/// <list type="bullet">
/// <item>Sahiplik (GW_OWNER) elemiyor -- hatanin kendisi oydu.</item>
/// <item>Surec (pid) de elemiyor: Excel dosya secim diyalogu ORKA surecine ait
/// olmayabilir, "Transfer Edilecek Excel" penceresi pid'e bakilsa kacardi.</item>
/// </list>
/// </summary>
public class PencereBekleyici
{
    private readonly UIA3Automation _automation;
    private readonly AdimLogger _log;
    private readonly RobotConfig _cfg;

    // ORKA process id'leri her cagrida process listesi taramasini gerektirmesin.
    private List<int> _pidOnbellek = new();
    private DateTime _pidZamani = DateTime.MinValue;
    private static readonly TimeSpan PidOnbellekSuresi = TimeSpan.FromSeconds(5);

    public PencereBekleyici(UIA3Automation automation, AdimLogger log, RobotConfig cfg)
    {
        _automation = automation;
        _log = log;
        _cfg = cfg;
    }

    // ================== SAF MANTIK ==================
    // Asagidaki uc metot ekrana dokunmuyor: girdi pencere listesi, cikti karar.
    // Eslestirme kurali burada durdugu icin "sahipli popup elenir mi", "baska
    // surecin diyalogu bulunur mu" gibi sorular ORKA olmadan test edilebiliyor.

    /// <summary>
    /// 'Deger' icinde '|' ile birden fazla aday baslik verilebilir:
    ///   "Veri Transferi|Transfer Islemleri"
    /// ORKA surumden surume baslik degistirdigi icin tek basliga baglanmak kirilgan.
    /// </summary>
    public static string[] Adaylar(string parca)
        => (parca ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries |
                                    StringSplitOptions.TrimEntries);

    /// <summary>
    /// Basligi adaylardan birini iceren ilk GORUNUR pencere; yoksa null.
    /// Adaylar SIRAYLA denenir: "A|B" verildiginde once A'yi tasiyan pencere
    /// aranir, hicbiri yoksa B'ye gecilir.
    /// </summary>
    public static UstSeviyePencere? Esles(IReadOnlyList<UstSeviyePencere> pencereler, string parca)
    {
        foreach (var aday in Adaylar(parca))
        {
            var bulunan = pencereler.FirstOrDefault(p => p.Gorunur && p.BasligaUyuyor(aday));
            if (bulunan is not null) return bulunan;
        }

        return null;
    }

    /// <summary>
    /// Ekranda "duran" pencerelerin basliklari: gorunur ve basligi bos olmayanlar.
    /// Timeout mesajindaki "Ekrandakiler" listesi de buradan uretiliyor -- liste
    /// aramanin baktigi kumeden gelmezse hata ayiklarken yaniltir.
    /// </summary>
    public static List<string> Basliklar(IReadOnlyList<UstSeviyePencere> pencereler)
        => pencereler
            .Where(p => p.Gorunur && !string.IsNullOrWhiteSpace(p.Baslik))
            .Select(p => p.Baslik)
            .ToList();

    // ================== TARAMA ==================

    /// <summary>Masaustundeki tum ust seviye pencereler; tek tarama noktasi.</summary>
    private List<UstSeviyePencere> Tara()
    {
        try
        {
            return OrkaPenceresi.TumUstSeviyePencereler();
        }
        catch (Exception ex)
        {
            _log.Uyari($"Pencere listesi alinamadi: {ex.Message}");
            return new List<UstSeviyePencere>();
        }
    }

    /// <summary>Ekrandaki gorunur pencerelerin basliklari.</summary>
    public List<string> TumPencereBasliklari() => Basliklar(Tara());

    /// <summary>
    /// Basligi 'parca' iceren pencereyi bul (buyuk/kucuk harf duyarsiz).
    /// Birden fazla aday varsa sirayla dener, ilk bulunani dondurur.
    ///
    /// IKI ASAMALI arama:
    ///   1. Ust seviye pencereler (EnumWindows) -- sahipli popup'lar dahil
    ///   2. Bulamazsa ORKA'ya ait pencerelerin ALT pencereleri (UIA)
    ///
    /// 2. asama artik yalnizca gercek COCUK pencereler (WS_CHILD) icin var:
    /// sahipli ust seviye pencereler 1. asamada zaten cikiyor. Kokleri de
    /// EnumWindows veriyor -- eskiden UIA masaustu cocuklarindan geliyordu ve
    /// ORKA hic listelenmedigi icin bu asama da bos donuyordu.
    /// </summary>
    public UstSeviyePencere? Bul(string parca)
    {
        var bulunan = Esles(Tara(), parca);
        if (bulunan is not null) return bulunan;

        foreach (var aday in Adaylar(parca))
        {
            var derin = DerinBul(aday);
            if (derin is not null) return derin;
        }

        return null;
    }

    /// <summary>Var mi diye bakar, beklemez. '|' ile aday listesi verilebilir.</summary>
    public bool VarMi(string parca) => Bul(parca) != null;

    /// <summary>
    /// Pencere gelene kadar bekler. Gelmezse TimeoutException firlatir.
    /// Timeout mesajinda DENENEN TUM ADAYLAR ve o an ekranda olan basliklar birlikte
    /// yazilir; ofiste "neyi aradi, ne vardi" sorusu tek satirda cevaplanabilsin diye.
    ///
    /// Dongude yalnizca ust seviye tarama donuyor (ucuz, saniyede iki kez).
    /// Alt pencere aramasi pahali oldugu icin sure dolduktan sonra SON BIR KEZ
    /// deneniyor: pes etmeden once bakilmamis bir yer kalmasin.
    /// </summary>
    public UstSeviyePencere Bekle(string parca, int timeoutSn)
    {
        var adaylar = Adaylar(parca);
        if (adaylar.Length == 0)
            throw new ArgumentException("Beklenecek pencere basligi bos.", nameof(parca));

        var bitis = DateTime.Now.AddSeconds(timeoutSn);
        _log.Bilgi($"Bekleniyor: {string.Join(" | ", adaylar.Select(a => $"'{a}'"))} " +
                   $"(max {timeoutSn} sn)");

        while (DateTime.Now < bitis)
        {
            var pencereler = Tara();

            foreach (var aday in adaylar)
            {
                var bulunan = Esles(pencereler, aday);
                if (bulunan is not null)
                {
                    _log.Bilgi($"Bulundu: '{bulunan.Baslik}'  (eslesen aday: '{aday}')");
                    return bulunan;
                }
            }

            Thread.Sleep(400);
        }

        foreach (var aday in adaylar)
        {
            var derin = DerinBul(aday);
            if (derin is not null)
            {
                _log.Bilgi($"Bulundu (alt pencere): '{derin.Baslik}'  (eslesen aday: '{aday}')");
                return derin;
            }
        }

        var mevcut = TumPencereBasliklari();
        var ekrandakiler = mevcut.Count == 0
            ? "(hic pencere okunamadi - ORKA yonetici modunda olabilir)"
            : string.Join(" | ", mevcut.Take(30));

        // Hem log'a hem exception mesajina yaz: log dosyasinda ayri satir olarak
        // durmasi, uzun baslik listelerinde okumayi kolaylastiriyor.
        _log.Hata($"TIMEOUT ({timeoutSn} sn). Denenen adaylar ({adaylar.Length}): " +
                  string.Join(" | ", adaylar));
        _log.Hata($"TIMEOUT. Ekrandaki pencereler ({mevcut.Count}): {ekrandakiler}");

        throw new TimeoutException(
            $"Beklenen pencere {timeoutSn} sn icinde gelmedi." + Environment.NewLine +
            $"  Denenen adaylar ({adaylar.Length}): {string.Join(" | ", adaylar)}" + Environment.NewLine +
            $"  Ekrandakiler ({mevcut.Count}): {ekrandakiler}");
    }

    /// <summary>
    /// Beklenmeyen bir uyari/hata penceresi acilmis mi kontrol eder.
    /// Delphi uygulamalarinda surpriz pencere cok cikar; her adimdan sonra bakilmali.
    /// </summary>
    public string? BeklenmeyenPencereVarMi(IEnumerable<string> anahtarlar)
    {
        var basliklar = TumPencereBasliklari();
        foreach (var anahtar in anahtarlar)
        {
            var bulunan = basliklar.FirstOrDefault(b =>
                b.Contains(anahtar, StringComparison.OrdinalIgnoreCase));
            if (bulunan != null) return bulunan;
        }
        return null;
    }

    // ================== ALT PENCERE (2. ASAMA) ==================

    /// <summary>
    /// ORKA'ya ait ust seviye pencerelerin ALTINDAKI pencereler.
    /// Sadece Window ve Pane tipleri taranir; Delphi formlarindaki binlerce
    /// alt kontrolu gezmek gereksiz yavaslatir.
    ///
    /// Tutamaci olmayan eleman kabul EDILMIYOR: olcu de tiklama da tutamactan
    /// gidiyor (<see cref="OrkaPenceresi.OlcuAl"/>), tutamacsiz bir eleman
    /// "bulundu" sayilsaydi bir sonraki adim sessizce yanlis yere tiklardi.
    /// </summary>
    private UstSeviyePencere? DerinBul(string aday)
    {
        foreach (var kok in OrkaKokPencereleri())
        {
            AutomationElement? kokEleman;
            try
            {
                kokEleman = _automation.FromHandle(kok.Tutamac);
            }
            catch (Exception ex)
            {
                _log.Uyari($"Alt pencere taranamadi ('{kok.Baslik}'): {ex.Message}");
                continue;
            }

            if (kokEleman is null) continue;

            try
            {
                var kosul = kokEleman.ConditionFactory.ByControlType(ControlType.Window)
                                     .Or(kokEleman.ConditionFactory.ByControlType(ControlType.Pane));

                var bulunan = kokEleman.FindAllDescendants(kosul)
                                       .FirstOrDefault(e => AdEsliyor(e, aday));

                if (bulunan is null) continue;

                var tutamac = Tutamac(bulunan);
                if (tutamac == IntPtr.Zero)
                {
                    _log.Uyari($"Alt pencere '{Ad(bulunan)}' bulundu ama Win32 tutamaci yok; " +
                               "olcu ve tiklama tutamaca bagli oldugu icin kullanilmiyor.");
                    continue;
                }

                _log.Bilgi($"Alt pencerede bulundu: '{Ad(bulunan)}' (kok pencere: '{kok.Baslik}')");

                return new UstSeviyePencere(
                    tutamac,
                    kok.Pid,
                    Ad(bulunan),
                    OrkaPenceresi.OlcuAl(tutamac),
                    Gorunur: true,
                    Sahip: IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _log.Uyari($"Alt pencere taranamadi ('{kok.Baslik}'): {ex.Message}");
            }
        }

        return null;
    }

    private static bool AdEsliyor(AutomationElement e, string aday)
    {
        try
        {
            return !string.IsNullOrEmpty(e.Name) &&
                   e.Name.Contains(aday, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string Ad(AutomationElement e)
    {
        try { return e.Name ?? ""; } catch { return "?"; }
    }

    /// <summary>UIA elemaninin Win32 tutamaci; okunamazsa <see cref="IntPtr.Zero"/>.</summary>
    private static IntPtr Tutamac(AutomationElement e)
    {
        try
        {
            return e.Properties.NativeWindowHandle.TryGetValue(out var h) ? h : IntPtr.Zero;
        }
        catch { return IntPtr.Zero; }
    }

    // ================== ORKA SURECI VE ODAK ==================

    /// <summary>
    /// ORKA'nin process id'leri. Iki kaynaktan toplanir:
    ///   1. OrkaPath'teki exe adi (ORKA henuz giris ekranindayken de bulunur)
    ///   2. Config'deki bilinen pencere basliklarini tasiyan pencerelerin process'i
    ///      (exe adi farkliysa ya da ORKA baska yoldan acildiysa kurtarir)
    /// Birkac saniyeligine onbellege alinir; her adimda process listesi taranmasin.
    /// </summary>
    public IReadOnlyList<int> OrkaProcessIdleri()
    {
        if (_pidOnbellek.Count > 0 && DateTime.Now - _pidZamani < PidOnbellekSuresi)
            return _pidOnbellek;

        var pidler = new HashSet<int>();

        try
        {
            var exeAdi = Path.GetFileNameWithoutExtension(_cfg.OrkaPath);
            if (!string.IsNullOrWhiteSpace(exeAdi))
                foreach (var p in Process.GetProcessesByName(exeAdi))
                {
                    try { pidler.Add(p.Id); } finally { p.Dispose(); }
                }
        }
        catch (Exception ex)
        {
            _log.Uyari($"ORKA process'i aranamadi: {ex.Message}");
        }

        var bilinenBasliklar = new[]
        {
            _cfg.Pencereler.AnaEkran,
            _cfg.Pencereler.GirisEkrani,
            _cfg.Pencereler.SubeSecim
        }.Where(b => !string.IsNullOrWhiteSpace(b)).ToArray();

        foreach (var w in Tara())
        {
            if (w.Pid == 0 || !w.Gorunur) continue;
            if (!bilinenBasliklar.Any(b => w.BasligaUyuyor(b))) continue;
            pidler.Add(w.Pid);
        }

        _pidOnbellek = pidler.ToList();
        _pidZamani = DateTime.Now;
        return _pidOnbellek;
    }

    /// <summary>ORKA process'ine ait ust seviye pencereler (derin aramanin kokleri).</summary>
    private List<UstSeviyePencere> OrkaKokPencereleri()
    {
        var pidler = OrkaProcessIdleri();
        if (pidler.Count == 0) return new List<UstSeviyePencere>();

        return Tara().Where(w => w.Pid != 0 && pidler.Contains(w.Pid)).ToList();
    }

    /// <summary>
    /// Odaktaki eleman ORKA'nin process'ine mi ait?
    ///
    /// <b>Artik tek basina karar olcutu DEGIL.</b> Surec cozunurlugu, "ORKA'nin
    /// hangi penceresi onde" sorusunu cevaplayamiyor: modul ekrani ile ORKA'nin
    /// bir diyalogu bu kontrole gore ayni sey ve odak duzeltme, tuslar yanlis
    /// pencereye giderken bile "sorun yok" diyip sifir gecikmeyle donuyordu.
    /// Karar <see cref="OndeMi"/> ile PENCERE duzeyinde veriliyor; bu metot artik
    /// yalnizca TESHIS icin: hedef pencere hic secilemediginde uyarinin tonunu
    /// belirliyor ("odak baska uygulamada" ile "odak ORKA'da ama hangi pencerede
    /// bilinmiyor" ayni sey degil).
    /// </summary>
    public bool OdakOrkadaMi()
    {
        var pidler = OrkaProcessIdleri();
        if (pidler.Count == 0) return false;

        try
        {
            var odak = _automation.FocusedElement();
            if (odak is null) return false;
            var pid = odak.Properties.ProcessId.TryGetValue(out var p) ? p : (int?)null;
            return pid.HasValue && pidler.Contains(pid.Value);
        }
        catch { return false; }
    }

    /// <summary>
    /// ON PLANDAKI pencere ORKA surecine mi ait?
    ///
    /// Odak duzeltmenin "dokunma" karari buna dayaniyor: ORKA'nin kendi actigi bir
    /// diyalog (F7 firma listesi gibi) hicbir adim tarafindan beklenmemis olabilir,
    /// yani hedef pencere listesinde gorunmez -- ama odak zaten DOGRU yerdedir.
    /// Baslik degil PID sorusu: diyalogun basligini bilmek gerekmiyor.
    /// </summary>
    public bool OnPlanOrkadaMi(IntPtr onPlandaki)
    {
        var pid = OrkaPenceresi.PencereSureci(onPlandaki);
        if (pid == 0) return false;

        return OrkaProcessIdleri().Contains(pid);
    }

    /// <summary>
    /// One getirilecek ORKA penceresi: once ana ekran, sonra giris ekrani,
    /// sonra sube secim, olmadi ORKA process'inin herhangi bir penceresi.
    /// </summary>
    public UstSeviyePencere? OrkaOnPenceresi()
    {
        var pencereler = Tara();

        foreach (var baslik in new[] { _cfg.Pencereler.AnaEkran,
                                       _cfg.Pencereler.GirisEkrani,
                                       _cfg.Pencereler.SubeSecim })
        {
            if (string.IsNullOrWhiteSpace(baslik)) continue;
            var el = Esles(pencereler, baslik);
            if (el != null) return el;
        }

        return OrkaKokPencereleri().FirstOrDefault(w => w.Gorunur && w.Olcu.Gecerli);
    }

    // ================== ONE GETIRME ==================

    /// <summary>
    /// Pencereyi sadece one getirir, boyutuna dokunmaz.
    /// Otomatik odak duzeltmede kullaniliyor: giris ekrani gibi kucuk diyaloglari
    /// zorla buyutmek yanlis olur.
    ///
    /// <b>Sonuc yutulmuyor:</b> <see cref="OrkaPenceresi.OneGetir"/> pencerenin
    /// gercekten one geldigini dogruluyor; dogrulanamadiginda gorev DURMUYOR ama
    /// log'a uyari dusuyor. Sessiz kalinsaydi bir sonraki adimin ilk tusu neden
    /// kayboldu sorusu yine cevapsiz kalirdi.
    /// </summary>
    /// <returns>Pencere on plana geldiyse true.</returns>
    public bool OneGetir(UstSeviyePencere pencere)
    {
        try
        {
            var rapor = OrkaPenceresi.OneGetir(pencere.Tutamac);

            if (!rapor.CagriKabulEdildi && !rapor.ZatenOndeydi)
                _log.Uyari($"'{pencere.Baslik}': SetForegroundWindow REDDETTI " +
                           "(foreground lock). Windows baska bir pencerenin one gelmesine izin vermiyor.");

            if (!rapor.OndeMi)
            {
                _log.Uyari($"'{pencere.Baslik}' {rapor.GecenMs} ms icinde on plana GELMEDI. " +
                           "Adima devam ediliyor; gonderilen tuslar baska pencereye gidebilir.");
                return false;
            }

            if (!rapor.ZatenOndeydi)
                _log.Bilgi($"'{pencere.Baslik}' on plana geldi ({rapor.GecenMs} ms).");

            return true;
        }
        catch (Exception ex)
        {
            _log.Uyari($"Pencere one getirilemedi ('{pencere.Baslik}'): {ex.Message}");
            return false;
        }
    }

    // ================== ALT PENCERE (SINIF ADI) ==================

    /// <summary>
    /// ORKA'nin ust seviye pencerelerinin altindaki TUM kontroller, sinif adlariyla.
    ///
    /// <b>Neden baslik yetmiyor:</b> modul/sekme gecisi ORKA'da yeni bir ust seviye
    /// pencere ACMIYOR ve ana pencerenin basligi da degismiyor. "TRANSFERLER'e
    /// girildi mi" sorusunu baslik bakan <see cref="VarMi"/> cevaplayamiyor --
    /// robot giremedigi halde devam edip yanlis ekranda tikliyordu. Alt pencere
    /// agaci UIA'ya kapali ORKA'da okunabilen tek yapisal sinyal.
    /// </summary>
    public List<OrkaPenceresi.AltPencere> OrkaAltPencereleri()
    {
        var sonuc = new List<OrkaPenceresi.AltPencere>();

        foreach (var kok in OrkaKokPencereleri())
        {
            if (!kok.Gorunur) continue;
            sonuc.AddRange(OrkaPenceresi.AltPencereler(kok.Tutamac));
        }

        return sonuc;
    }

    /// <summary>
    /// Sinif adi (ya da kontrol basligi) verilen parcalardan birini iceren
    /// GORUNUR bir alt kontrol var mi?
    ///
    /// Yalniz GORUNUR olanlara bakiliyor: kapali bir sekmenin kontrolleri agacta
    /// KALIR, sadece WS_VISIBLE'lari duser. Gorunurluk atlanirsa "sekme acik mi"
    /// sorusu her zaman "evet" cevabini verirdi.
    ///
    /// <paramref name="adaylar"/> '|' ile ayrilmis olabilir -- BeklePencere/Dogrula
    /// ile ayni kalip.
    /// </summary>
    public OrkaPenceresi.AltPencere? AltPencereBul(string adaylar)
    {
        var parcalar = adaylar.Split('|', StringSplitOptions.RemoveEmptyEntries |
                                          StringSplitOptions.TrimEntries);
        if (parcalar.Length == 0) return null;

        var hepsi = OrkaAltPencereleri();

        foreach (var parca in parcalar)
        {
            var bulunan = hepsi.FirstOrDefault(
                a => a.Gorunur &&
                     (a.SinifAdi.Contains(parca, StringComparison.OrdinalIgnoreCase) ||
                      (!string.IsNullOrEmpty(a.Baslik) &&
                       a.Baslik.Contains(parca, StringComparison.OrdinalIgnoreCase))));

            if (bulunan != null) return bulunan;
        }

        return null;
    }

    /// <summary>
    /// Alt kontrol gelene kadar bekler; gelirse dondurur, gelmezse null.
    ///
    /// Yoklamali: sekme cizimi ORKA'da bazen bir kac yuz ms suruyor ve tek
    /// atislik bir kontrol "acilmadi" derdi. Sure dolunca DURDURMA karari
    /// burada degil, cagiran adimda.
    /// </summary>
    public OrkaPenceresi.AltPencere? AltPencereBekle(string adaylar, int timeoutSn)
    {
        var bitis = DateTime.Now.AddSeconds(Math.Max(1, timeoutSn));

        while (true)
        {
            var bulunan = AltPencereBul(adaylar);
            if (bulunan != null) return bulunan;
            if (DateTime.Now >= bitis) return null;

            Thread.Sleep(250);
        }
    }

    /// <summary>
    /// Alt kontrollerin sinif adi dokumu: "sinif x adet" satirlari.
    ///
    /// Iki is yapiyor: kesif ("hangi sinif adi Veri Transferi'ne ozel") ve
    /// basarisizlik mesaji ("bekleneni bulamadim, ekranda BUNLAR vardi").
    /// Ayni kalip <see cref="Bekle"/> zaman asimi mesajinda da var.
    /// </summary>
    public List<string> AltPencereOzeti(int enFazla = 40)
        => OrkaAltPencereleri()
            .Where(a => a.Gorunur)
            .GroupBy(a => a.SinifAdi, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(enFazla)
            .Select(g => $"{g.Key} x{g.Count()}")
            .ToList();

    /// <summary>
    /// Metni OLAN gorunur alt kontroller: "SinifAdi &lt;- 'metin'" satirlari.
    ///
    /// <see cref="AltPencereOzeti"/> yalnizca sinif adlarini sayiyor; bir
    /// dogrulama METNE gore arandiginda ("Transfer Islemi Tamamlandi") o dokum
    /// "neyi bulamadim" sorusunu cevaplamiyor -- ekranda hangi metinlerin
    /// oldugunu gormek gerekiyor.
    /// </summary>
    public List<string> AltPencereMetinOzeti(int enFazla = 25)
        => OrkaAltPencereleri()
            .Where(a => a.Gorunur && !string.IsNullOrWhiteSpace(a.Baslik))
            .Take(enFazla)
            .Select(a => $"{a.SinifAdi} <- '{a.Baslik}'")
            .ToList();

    /// <summary>Verilen pencere SU AN on planda mi? Surec degil, PENCERE sorusu.</summary>
    public static bool OndeMi(UstSeviyePencere? pencere)
        => pencere is not null &&
           pencere.Tutamac != IntPtr.Zero &&
           OrkaPenceresi.OnPencere() == pencere.Tutamac;

    /// <summary>
    /// Pencereyi one getirir ve buyutur. Layout sabitlemek icin onemli:
    /// Tikla adimindaki oranin paydasi bu dikdortgen.
    ///
    /// One getirme Win32 (tutamac), buyutme UIA WindowPattern ile: pencere
    /// bulmak UIA'da calismiyor (bkz. sinif basligi) ama tutamaci elde olan bir
    /// pencerenin pattern'i okunabiliyor ve maximize davranisi degismesin.
    /// </summary>
    public bool OneGetirVeBuyut(UstSeviyePencere pencere)
    {
        var onde = OneGetir(pencere);

        try
        {
            var el = _automation.FromHandle(pencere.Tutamac);
            var win = el?.AsWindow();
            if (win != null && win.Patterns.Window.IsSupported &&
                win.Patterns.Window.Pattern.CanMaximize)
            {
                win.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

                // Buyutme YENI bir mesaj dalgasi baslatiyor: pencere yeniden
                // boyutlanip tile'lari bastan ciziyor. OneGetir icindeki bariyer
                // bundan ONCE calisti, yani onun garantisi burada gecersiz.
                // Bariyer olmadan bir sonraki adimin ilk tusu cizim bitmeden
                // gidiyor ve YUTULUYOR -- modul ekraninda "SAG x3" istenip iki
                // adim ilerlenmesinin sebeplerinden biri bu.
                FlaUI.Core.Input.Wait.UntilResponsive(pencere.Tutamac);
            }
        }
        catch (Exception ex)
        {
            _log.Uyari($"Pencere buyutulemedi ('{pencere.Baslik}'): {ex.Message}");
        }

        return onde;
    }
}
