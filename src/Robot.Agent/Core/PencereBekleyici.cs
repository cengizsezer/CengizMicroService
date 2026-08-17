using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using PkfRobot.Config;

namespace PkfRobot.Core;

/// <summary>
/// Thread.Sleep ile ilerleyen robot ilk yavas gunde coker.
/// Bu sinif "beklenen pencere gelene kadar bekle" mantigini saglar.
/// ORKA'nin ic kontrolleri UIA'ya kapali ama PENCERE BASLIKLARI okunabiliyor,
/// dogrulamanin tamami bunun uzerine kurulu.
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

    /// <summary>Masaustundeki tum ust seviye pencerelerin basliklari.</summary>
    public List<string> TumPencereBasliklari()
    {
        try
        {
            return UstSeviyePencereler()
                .Select(Ad)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        }
        catch (Exception ex)
        {
            _log.Uyari($"Pencere listesi alinamadi: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// 'Deger' icinde '|' ile birden fazla aday baslik verilebilir:
    ///   "Veri Transferi|Transfer Islemleri"
    /// ORKA surumden surume baslik degistirdigi icin tek basliga baglanmak kirilgan.
    /// </summary>
    public static string[] Adaylar(string parca)
        => (parca ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries |
                                    StringSplitOptions.TrimEntries);

    /// <summary>
    /// Basligi 'parca' iceren pencereyi bul (buyuk/kucuk harf duyarsiz).
    /// Birden fazla aday varsa sirayla dener, ilk bulunani dondurur.
    ///
    /// IKI ASAMALI arama:
    ///   1. Ust seviye pencereler (hizli, cogu durumda buraya duser)
    ///   2. Bulamazsa ORKA process'ine ait pencerelerin ALT pencereleri
    ///
    /// 2. asama sart: ORKA'nin "Firma Sifresini Giriniz." popup'i ana pencerenin
    /// ALT penceresi. Masaustunun dogrudan cocugu olmadigi icin sadece ust seviyeye
    /// bakildiginda ekranda dururken bile bulunamiyordu ve 40 sn timeout oluyordu.
    /// </summary>
    public AutomationElement? Bul(string parca)
    {
        var adaylar = Adaylar(parca);

        foreach (var aday in adaylar)
        {
            var el = TekAdayBul(aday);
            if (el != null) return el;
        }

        // Ust seviyede yok. ORKA'nin alt pencerelerine in.
        foreach (var aday in adaylar)
        {
            var el = TekAdayDerinBul(aday);
            if (el != null) return el;
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

    /// <summary>1. asama: sadece masaustunun dogrudan cocuklari.</summary>
    private AutomationElement? TekAdayBul(string aday)
    {
        try
        {
            return UstSeviyePencereler().FirstOrDefault(e => AdEsliyor(e, aday));
        }
        catch { return null; }
    }

    /// <summary>
    /// 2. asama: ORKA'ya ait ust seviye pencerelerin ALTINDAKI pencereler.
    /// Sadece Window ve Pane tipleri taranir; Delphi formlarindaki binlerce
    /// alt kontrolu gezmek gereksiz yavaslatir.
    /// </summary>
    private AutomationElement? TekAdayDerinBul(string aday)
    {
        foreach (var kok in OrkaKokPencereleri())
        {
            try
            {
                var kosul = kok.ConditionFactory.ByControlType(ControlType.Window)
                               .Or(kok.ConditionFactory.ByControlType(ControlType.Pane));

                var bulunan = kok.FindAllDescendants(kosul)
                                 .FirstOrDefault(e => AdEsliyor(e, aday));

                if (bulunan != null)
                {
                    _log.Bilgi($"Alt pencerede bulundu: '{bulunan.Name}' " +
                               $"(kok pencere: '{Ad(kok)}')");
                    return bulunan;
                }
            }
            catch (Exception ex)
            {
                _log.Uyari($"Alt pencere taranamadi ('{Ad(kok)}'): {ex.Message}");
            }
        }
        return null;
    }

    private AutomationElement[] UstSeviyePencereler()
    {
        try { return _automation.GetDesktop().FindAllChildren(); }
        catch { return Array.Empty<AutomationElement>(); }
    }

    private static string Ad(AutomationElement e)
    {
        try { return e.Name ?? ""; } catch { return "?"; }
    }

    private static int? Pid(AutomationElement e)
    {
        try { return e.Properties.ProcessId.Value; } catch { return null; }
    }

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

        foreach (var w in UstSeviyePencereler())
        {
            if (!bilinenBasliklar.Any(b => AdEsliyor(w, b))) continue;
            var pid = Pid(w);
            if (pid.HasValue) pidler.Add(pid.Value);
        }

        _pidOnbellek = pidler.ToList();
        _pidZamani = DateTime.Now;
        return _pidOnbellek;
    }

    /// <summary>ORKA process'ine ait ust seviye pencereler (derin aramanin kokleri).</summary>
    private List<AutomationElement> OrkaKokPencereleri()
    {
        var pidler = OrkaProcessIdleri();
        if (pidler.Count == 0) return new List<AutomationElement>();

        return UstSeviyePencereler()
            .Where(w => { var p = Pid(w); return p.HasValue && pidler.Contains(p.Value); })
            .ToList();
    }

    /// <summary>
    /// Odaktaki eleman ORKA'nin process'ine mi ait?
    /// Baslik yerine PROCESS bakiliyor: ORKA'nin kendi acdigi Excel dosya secim
    /// diyalogu da ORKA sayilsin, bosuna one getirme yapilmasin.
    /// </summary>
    public bool OdakOrkadaMi()
    {
        var pidler = OrkaProcessIdleri();
        if (pidler.Count == 0) return false;

        try
        {
            var odak = _automation.FocusedElement();
            var pid = odak == null ? null : Pid(odak);
            return pid.HasValue && pidler.Contains(pid.Value);
        }
        catch { return false; }
    }

    /// <summary>
    /// One getirilecek ORKA penceresi: once ana ekran, sonra giris ekrani,
    /// sonra sube secim, olmadi ORKA process'inin herhangi bir penceresi.
    /// </summary>
    public AutomationElement? OrkaOnPenceresi()
    {
        foreach (var baslik in new[] { _cfg.Pencereler.AnaEkran,
                                       _cfg.Pencereler.GirisEkrani,
                                       _cfg.Pencereler.SubeSecim })
        {
            if (string.IsNullOrWhiteSpace(baslik)) continue;
            var el = TekAdayBul(baslik);
            if (el != null) return el;
        }
        return OrkaKokPencereleri().FirstOrDefault();
    }

    /// <summary>
    /// Pencere gelene kadar bekler. Gelmezse TimeoutException firlatir.
    /// Timeout mesajinda DENENEN TUM ADAYLAR ve o an ekranda olan basliklar birlikte
    /// yazilir; ofiste "neyi aradi, ne vardi" sorusu tek satirda cevaplanabilsin diye.
    /// </summary>
    public AutomationElement Bekle(string parca, int timeoutSn)
    {
        var adaylar = Adaylar(parca);
        if (adaylar.Length == 0)
            throw new ArgumentException("Beklenecek pencere basligi bos.", nameof(parca));

        var bitis = DateTime.Now.AddSeconds(timeoutSn);
        _log.Bilgi($"Bekleniyor: {string.Join(" | ", adaylar.Select(a => $"'{a}'"))} " +
                   $"(max {timeoutSn} sn)");

        while (DateTime.Now < bitis)
        {
            foreach (var aday in adaylar)
            {
                var el = TekAdayBul(aday);
                if (el != null)
                {
                    _log.Bilgi($"Bulundu: '{el.Name}'  (eslesen aday: '{aday}')");
                    return el;
                }
            }
            Thread.Sleep(400);
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

    /// <summary>Var mi diye bakar, beklemez. '|' ile aday listesi verilebilir.</summary>
    public bool VarMi(string parca) => Bul(parca) != null;

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

    /// <summary>
    /// Pencereyi sadece one getirir, boyutuna dokunmaz.
    /// Otomatik odak duzeltmede kullaniliyor: giris ekrani gibi kucuk diyaloglari
    /// zorla buyutmek yanlis olur.
    /// </summary>
    public void OneGetir(AutomationElement el)
    {
        try
        {
            el.Focus();
            el.SetForeground();
        }
        catch (Exception ex)
        {
            _log.Uyari($"Pencere one getirilemedi: {ex.Message}");
        }
    }

    /// <summary>Pencereyi one getirir ve buyutur. Layout sabitlemek icin onemli.</summary>
    public void OneGetirVeBuyut(AutomationElement el)
    {
        try
        {
            el.Focus();
            el.SetForeground();
            var win = el.AsWindow();
            if (win != null && win.Patterns.Window.IsSupported)
            {
                if (win.Patterns.Window.Pattern.CanMaximize)
                    win.Patterns.Window.Pattern.SetWindowVisualState(
                        FlaUI.Core.Definitions.WindowVisualState.Maximized);
            }
        }
        catch (Exception ex)
        {
            _log.Uyari($"Pencere one getirilemedi: {ex.Message}");
        }
    }
}
