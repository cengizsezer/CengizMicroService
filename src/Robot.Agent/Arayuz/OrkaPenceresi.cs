using System.Diagnostics;
using System.Runtime.Versioning;
using PkfRobot.Ayarlar;

namespace PkfRobot.Arayuz;

/// <summary>Ekrandaki ORKA penceresinin o anki hali.</summary>
/// <param name="Tutamac">Pencere tutamaci; bulunamadiysa <see cref="IntPtr.Zero"/>.</param>
/// <param name="Olcu">Pencerenin ekrandaki dikdortgeni.</param>
/// <param name="TamEkran">Maximize durumda mi?</param>
/// <param name="SimgeDurumunda">Gorev cubuguna indirilmis mi?</param>
/// <param name="Surecler">ORKA'ya ait surec kimlikleri.</param>
public record OrkaPencereDurumu(
    IntPtr Tutamac,
    PencereOlcusu Olcu,
    bool TamEkran,
    bool SimgeDurumunda,
    IReadOnlyCollection<int> Surecler)
{
    public bool Bulundu => Tutamac != IntPtr.Zero;

    public static OrkaPencereDurumu Yok(IReadOnlyCollection<int>? surecler = null)
        => new(IntPtr.Zero, default, false, false, surecler ?? Array.Empty<int>());
}

/// <summary>
/// ORKA penceresini surec adindan bulur, olcusunu okur, one getirir.
///
/// <b>Neden baslik degil surec:</b> ORKA'nin pencere basligi surume ve acik
/// firmaya gore degisiyor (<c>ORKA_0001_2026</c>); surec adi sabit. Ajan
/// tarafinda ORKA'nin acik olup olmadigi da ayni sebeple surece bakilarak
/// belirleniyor (bkz. <see cref="PkfRobot.Ajan.OrkaSureci"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public class OrkaPenceresi
{
    private readonly string _surecAdi;

    public OrkaPenceresi(string surecAdi) => _surecAdi = surecAdi;

    /// <summary>ORKA'nin su anki hali. ORKA kapaliysa <see cref="OrkaPencereDurumu.Bulundu"/> false.</summary>
    public OrkaPencereDurumu Durum()
    {
        var surecler = new List<int>();
        var tutamac = IntPtr.Zero;

        Process[] bulunanlar;
        try
        {
            bulunanlar = Process.GetProcessesByName(_surecAdi);
        }
        catch (Exception)
        {
            return OrkaPencereDurumu.Yok();
        }

        try
        {
            foreach (var surec in bulunanlar)
            {
                surecler.Add(surec.Id);

                // Ana penceresi olmayan surec de ORKA sayiliyor (giris ekrani
                // henuz acilmamis olabilir): surec listesi tiklama denetimi icin
                // gerekli, tutamac ise yalniz olcu icin.
                if (tutamac == IntPtr.Zero && surec.MainWindowHandle != IntPtr.Zero)
                    tutamac = surec.MainWindowHandle;
            }
        }
        finally
        {
            foreach (var s in bulunanlar) s.Dispose();
        }

        if (tutamac == IntPtr.Zero || !Win32.IsWindow(tutamac))
            return OrkaPencereDurumu.Yok(surecler);

        var olcu = Olcu(tutamac);
        return new OrkaPencereDurumu(
            tutamac,
            olcu,
            Win32.IsZoomed(tutamac),
            Win32.IsIconic(tutamac),
            surecler);
    }

    /// <summary>Pencereyi one getirir; simge durumundaysa once geri acar.</summary>
    public static void OneGetir(IntPtr tutamac)
    {
        if (tutamac == IntPtr.Zero) return;

        if (Win32.IsIconic(tutamac)) Win32.ShowWindow(tutamac, Win32.SW_RESTORE);
        Win32.SetForegroundWindow(tutamac);
    }

    internal static PencereOlcusu Olcu(IntPtr tutamac)
    {
        if (!Win32.GetWindowRect(tutamac, out var r)) return default;
        return new PencereOlcusu(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>Verilen ekran noktasindaki pencerenin surec kimligi; okunamazsa null.</summary>
    public static int? NoktadakiSurec(int x, int y)
    {
        var nokta = new Win32.POINT { X = x, Y = y };
        var pencere = Win32.WindowFromPoint(nokta);
        if (pencere == IntPtr.Zero) return null;

        // Tiklanan sey bir alt kontrol olabilir; surec sorusu ust pencereden
        // sorulmali.
        var kok = Win32.GetAncestor(pencere, Win32.GA_ROOT);
        if (kok != IntPtr.Zero) pencere = kok;

        Win32.GetWindowThreadProcessId(pencere, out var pid);
        return pid == 0 ? null : (int)pid;
    }

    /// <summary>Tiklama anini <see cref="KoordinatSecimi"/>'nin bekledigi bicime cevirir.</summary>
    public TiklamaOrtami Ortam(int mutlakX, int mutlakY)
    {
        var durum = Durum();

        return new TiklamaOrtami(
            mutlakX,
            mutlakY,
            durum.Bulundu ? durum.Olcu : null,
            NoktadakiSurec(mutlakX, mutlakY),
            durum.Surecler,
            durum.TamEkran);
    }
}
