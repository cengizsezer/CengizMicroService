using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace PkfRobot.Core;

/// <summary>
/// ORKA'nin ic kontrolleri UIA'ya kapali oldugu icin tum etkilesim klavyeden.
/// Iyi haber: Delphi/VCL klavye navigasyonunu iyi destekliyor.
/// </summary>
public static class Klavye
{
    public static int VarsayilanBeklemeMs { get; set; } = 150;

    public static void Yaz(string metin, int? beklemeMs = null)
    {
        Keyboard.Type(metin);
        Thread.Sleep(beklemeMs ?? VarsayilanBeklemeMs);
    }

    public static void Tus(string tusAdi, int adet = 1, int? beklemeMs = null)
    {
        var tus = Cozumle(tusAdi);
        for (int i = 0; i < adet; i++)
        {
            Keyboard.Press(tus);
            Thread.Sleep(beklemeMs ?? VarsayilanBeklemeMs);
        }
    }

    /// <summary>"CTRL+F", "CTRL+A", "ALT+F4" gibi kombinasyonlar.</summary>
    public static void Kisayol(string kombinasyon, int? beklemeMs = null)
    {
        var parcalar = kombinasyon.Split('+', StringSplitOptions.RemoveEmptyEntries |
                                            StringSplitOptions.TrimEntries);
        var tuslar = parcalar.Select(Cozumle).ToArray();

        if (tuslar.Length == 1)
            Keyboard.Press(tuslar[0]);
        else
            Keyboard.TypeSimultaneously(tuslar);

        Thread.Sleep(beklemeMs ?? VarsayilanBeklemeMs);
    }

    /// <summary>Arama kutusunda mevcut metnin uzerine yazmamak icin: Ctrl+A sonra yaz.</summary>
    public static void TemizleVeYaz(string metin, int? beklemeMs = null)
    {
        Kisayol("CTRL+A", 100);
        Yaz(metin, beklemeMs);
    }

    private static VirtualKeyShort Cozumle(string ad)
    {
        var t = ad.Trim().ToUpperInvariant();

        return t switch
        {
            "ENTER" or "RETURN" => VirtualKeyShort.ENTER,
            "TAB"               => VirtualKeyShort.TAB,
            "ESC" or "ESCAPE"   => VirtualKeyShort.ESC,
            "SPACE"             => VirtualKeyShort.SPACE,
            "BACKSPACE"         => VirtualKeyShort.BACK,
            "DELETE" or "DEL"   => VirtualKeyShort.DELETE,
            "HOME"              => VirtualKeyShort.HOME,
            "END"               => VirtualKeyShort.END,
            "RIGHT" or "SAG"    => VirtualKeyShort.RIGHT,
            "LEFT"  or "SOL"    => VirtualKeyShort.LEFT,
            "UP"    or "YUKARI" => VirtualKeyShort.UP,
            "DOWN"  or "ASAGI"  => VirtualKeyShort.DOWN,
            "CTRL" or "CONTROL" => VirtualKeyShort.CONTROL,
            "ALT"               => VirtualKeyShort.ALT,
            "SHIFT"             => VirtualKeyShort.SHIFT,
            "F1"  => VirtualKeyShort.F1,
            "F2"  => VirtualKeyShort.F2,
            "F3"  => VirtualKeyShort.F3,
            "F4"  => VirtualKeyShort.F4,
            "F5"  => VirtualKeyShort.F5,
            "F6"  => VirtualKeyShort.F6,
            "F7"  => VirtualKeyShort.F7,
            "F8"  => VirtualKeyShort.F8,
            "F9"  => VirtualKeyShort.F9,
            "F10" => VirtualKeyShort.F10,
            "F11" => VirtualKeyShort.F11,
            "F12" => VirtualKeyShort.F12,
            _ when t.Length == 1 && char.IsLetter(t[0]) =>
                Enum.Parse<VirtualKeyShort>("KEY_" + t),
            _ when t.Length == 1 && char.IsDigit(t[0]) =>
                Enum.Parse<VirtualKeyShort>("KEY_" + t),
            _ => throw new ArgumentException($"Bilinmeyen tus: {ad}")
        };
    }
}
