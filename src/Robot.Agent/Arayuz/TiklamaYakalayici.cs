using System.Runtime.Versioning;

namespace PkfRobot.Arayuz;

/// <summary>Tiklama yakalama nasil bitti?</summary>
public enum YakalamaSonu
{
    Tiklandi,
    IptalEdildi
}

/// <summary>
/// Ekranin herhangi bir yerine yapilacak <b>tek</b> sol tiklamayi yakalar ve
/// tiklamanin altindaki uygulamaya <b>gitmesini engeller</b>.
///
/// <b>Neden yutuluyor:</b> kullanici koordinat secerken ORKA'nin o dugmeye
/// gercekten basmasi istenmiyor. Kalibrasyon sirasinda ORKA'da bir menu acilsa
/// ya da bir kayit degisse, olcum yapan kisi farkinda olmadan veri degistirmis
/// olurdu.
///
/// <b>Neden dusuk seviye kanca:</b> tiklama baska bir uygulamanin (ORKA)
/// penceresine gidiyor; kendi formumuza gelen olaylarla yakalanamaz. Kanca
/// mesaj dongusu olan bir is parcaciginda kurulmali -- WinForms'un ana is
/// parcacigi bunu sagliyor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TiklamaYakalayici : IDisposable
{
    // Kanca temsilcileri alan olarak tutuluyor: yerel degiskende kalsalardi
    // cop toplayici onlari toplar ve Windows olmayan bir adrese cagri yapardi.
    private readonly Win32.HookProc _fareKancasi;
    private readonly Win32.HookProc _klavyeKancasi;

    private IntPtr _fare = IntPtr.Zero;
    private IntPtr _klavye = IntPtr.Zero;
    private bool _bitti;

    /// <summary>Tiklama yakalandi: ekran koordinati.</summary>
    public event Action<int, int>? Tiklandi;

    /// <summary>Esc ya da sag tik: secim iptal edildi.</summary>
    public event Action? Iptal;

    public TiklamaYakalayici()
    {
        _fareKancasi = FareOlayi;
        _klavyeKancasi = KlavyeOlayi;
    }

    public bool Dinliyor => _fare != IntPtr.Zero;

    public void Basla()
    {
        if (Dinliyor) return;

        _bitti = false;

        var modul = Win32.GetModuleHandle(null);
        _fare = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _fareKancasi, modul, 0);
        _klavye = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _klavyeKancasi, modul, 0);

        if (_fare == IntPtr.Zero)
        {
            Dur();
            throw new InvalidOperationException(
                "Tiklama yakalanamadi: fare kancasi kurulamadi. ORKA yonetici modunda " +
                "calisiyorsa PkfRobot'u da yonetici olarak calistirmak gerekebilir.");
        }
    }

    public void Dur()
    {
        if (_fare != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_fare);
            _fare = IntPtr.Zero;
        }

        if (_klavye != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_klavye);
            _klavye = IntPtr.Zero;
        }
    }

    private IntPtr FareOlayi(int kod, IntPtr wParam, IntPtr lParam)
    {
        if (kod < 0 || _bitti) return Win32.CallNextHookEx(IntPtr.Zero, kod, wParam, lParam);

        var mesaj = (int)wParam;

        if (mesaj == Win32.WM_RBUTTONDOWN)
        {
            _bitti = true;
            Iptal?.Invoke();
            return 1;
        }

        if (mesaj != Win32.WM_LBUTTONDOWN)
            return Win32.CallNextHookEx(IntPtr.Zero, kod, wParam, lParam);

        var veri = System.Runtime.InteropServices.Marshal
            .PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);

        _bitti = true;
        Tiklandi?.Invoke(veri.pt.X, veri.pt.Y);

        // 1 dondurmek "bu mesaji kimseye iletme" demek: tiklama ORKA'ya ulasmaz.
        return 1;
    }

    private IntPtr KlavyeOlayi(int kod, IntPtr wParam, IntPtr lParam)
    {
        if (kod < 0 || _bitti) return Win32.CallNextHookEx(IntPtr.Zero, kod, wParam, lParam);

        var mesaj = (int)wParam;
        if (mesaj is not (Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN))
            return Win32.CallNextHookEx(IntPtr.Zero, kod, wParam, lParam);

        var veri = System.Runtime.InteropServices.Marshal
            .PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);

        if (veri.vkCode != Win32.VK_ESCAPE)
            return Win32.CallNextHookEx(IntPtr.Zero, kod, wParam, lParam);

        _bitti = true;
        Iptal?.Invoke();
        return 1;
    }

    public void Dispose() => Dur();
}
