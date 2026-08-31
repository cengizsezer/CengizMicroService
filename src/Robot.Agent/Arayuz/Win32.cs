using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PkfRobot.Arayuz;

/// <summary>
/// Arayuzun ihtiyac duydugu Windows cagrilari.
///
/// UI Automation (FlaUI) yerine dogrudan Win32 kullaniliyor: burada gereken sey
/// pencerenin <b>dikdortgeni</b> ve tiklanan noktanin <b>hangi surece</b> ait
/// oldugu. UIA agacini kurmak bu iki soru icin agir kaliyor ve ORKA'nin gridi
/// zaten UIA'ya kapali (bkz. OKUBENI). Adim motoru UIA'yi kullanmaya devam
/// ediyor; burada ona dokunulmuyor.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Win32
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    internal const int WH_KEYBOARD_LL = 13;
    internal const int WH_MOUSE_LL = 14;

    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_SYSKEYDOWN = 0x0104;

    internal const int VK_ESCAPE = 0x1B;

    internal const int SW_HIDE = 0;
    internal const int SW_RESTORE = 9;

    internal const uint GA_ROOT = 2;

    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    internal static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);
}

/// <summary>
/// Konsol penceresini gizlemek icin kucuk yardimci.
///
/// Exe bilerek konsol alt sisteminde derleniyor (<c>--ajan</c>, <c>--probe</c>,
/// <c>--kalibre</c> ciktilari gorunsun diye). Arayuz modunda arkada bos bir
/// siyah pencere kalmasin diye o pencere gizleniyor -- kapatilmiyor: kapatmak
/// sureci de sonlandirirdi.
/// </summary>
[SupportedOSPlatform("windows")]
public static class KonsolPenceresi
{
    public static IntPtr Tutamac() => Win32.GetConsoleWindow();

    public static void Gizle(IntPtr tutamac) => Win32.ShowWindow(tutamac, Win32.SW_HIDE);
}
