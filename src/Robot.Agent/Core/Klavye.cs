using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace PkfRobot.Core;

/// <summary>
/// Klavyeye GERCEKTEN basan katman.
///
/// Ayri bir arayuz olmasinin tek sebebi test: SendInput'un ekranda ne yaptigi
/// dogrulanamaz ama "kac kere basildi, arasinda ne kadar beklendi, tus BIRAKILDI
/// mi" sorulari dogrulanabilir. Projedeki diger kaliplarla ayni cizgi: karar
/// mantigi ekrandan ayri durur.
/// </summary>
public interface IKlavyeSurucusu
{
    /// <summary>
    /// Tusa basip BIRAKIR (key-down + key-up).
    ///
    /// Birakma istege bagli degil: yalniz key-down gonderildiginde tus isletim
    /// sistemi acisindan basili kalir, sonraki basislar hedefe "auto-repeat"
    /// olarak gider ve bazi Delphi kontrolleri onlari yok sayar.
    /// </summary>
    void TusaBas(VirtualKeyShort tus);

    /// <summary>Metni yazar.</summary>
    void MetinYaz(string metin);

    /// <summary>Tuslara ayni anda basar (CTRL+A gibi), sonra ters sirada birakir.</summary>
    void BirlikteBas(IReadOnlyList<VirtualKeyShort> tuslar);

    /// <summary>Verilen sure kadar bekler.</summary>
    void Bekle(int ms);
}

/// <summary>Gercek klavye: FlaUI uzerinden SendInput.</summary>
public sealed class FlaUiKlavyeSurucusu : IKlavyeSurucusu
{
    // Keyboard.Type = press + release. Keyboard.Press YALNIZ key-down gonderir
    // (FlaUI 4.0.0, Keyboard.PressVirtualKeyCode -> SendInput isKeyDown:true) ve
    // burada onun kullanilmasi tuslarin hic birakilmamasina yol aciyordu.
    public void TusaBas(VirtualKeyShort tus) => Keyboard.Type(tus);

    public void MetinYaz(string metin) => Keyboard.Type(metin);

    public void BirlikteBas(IReadOnlyList<VirtualKeyShort> tuslar)
        => Keyboard.TypeSimultaneously(tuslar.ToArray());

    public void Bekle(int ms)
    {
        if (ms > 0) Thread.Sleep(ms);
    }
}

/// <summary>
/// <c>TemizleYaz</c> adiminin kutuyu NASIL bosalttigi.
///
/// Gorev dosyasindan seciliyor cunku cevap EKRANA gore degisiyor: ORKA'nin
/// kendi kutularinda CTRL calismiyor, Windows'un dosya secim diyalogunda
/// calisiyor.
/// </summary>
public enum TemizlemeYolu
{
    /// <summary>CTRL+A ile sec, uzerine yaz. Varsayilan; dosya diyalogunda calisiyor.</summary>
    CtrlA,

    /// <summary>END + SHIFT+HOME + DELETE. ORKA'nin kendi kutulari icin.</summary>
    SecVeSil
}

/// <summary>
/// ORKA'nin ic kontrolleri UIA'ya kapali oldugu icin tum etkilesim klavyeden.
/// Iyi haber: Delphi/VCL klavye navigasyonunu iyi destekliyor.
/// </summary>
public static class Klavye
{
    public static int VarsayilanBeklemeMs { get; set; } = 150;

    /// <summary>
    /// Tuslari gonderen surucu. Robot tek is parcaciginda calistigi icin duz bir
    /// statik alan yetiyor; testler kendi sahte surucusunu takip
    /// <see cref="SurucuyuSifirla"/> ile geri aliyor.
    /// </summary>
    public static IKlavyeSurucusu Surucu { get; set; } = new FlaUiKlavyeSurucusu();

    /// <summary>Gercek klavyeye geri doner.</summary>
    public static void SurucuyuSifirla() => Surucu = new FlaUiKlavyeSurucusu();

    // ================ GECICI TESHIS ================
    // Neden: "SAG x3 + ASAGI x1" gonderildigi halde ORKA'nin TRANSFERLER'e
    // girmemesi, tuslarin GONDERILMEDIGINI mi yoksa ORKA'nin onlari YUTTUGUNU mu
    // gosteriyor ayirt edilemiyordu -- adim log'u yalnizca "Tus RIGHT x3" yaziyor,
    // basislarin arasinda ne oldugunu yazmiyordu. Bu kanca her BASISI ayri satira
    // dusuruyor: kacinci basis, hangi tus, oncesinde ne kadar beklendi.
    //
    // Kanca olarak durmasinin sebebi: Klavye statik ve loglayiciyi tanimiyor;
    // testler de bu alani bos birakip eskisi gibi calisiyor. Teshis bitince
    // silinecek.
    public static Action<string>? Iz { get; set; }

    private static void IzYaz(string satir)
    {
        try { Iz?.Invoke(satir); }
        catch (Exception) { /* teshis satiri gorevi durdurmasin */ }
    }
    // ================ GECICI TESHIS SONU ================

    /// <summary>
    /// Metni yazar.
    ///
    /// <b>Iz satiri Tus/Kisayol ile ayni sebeple burada da var.</b> 06.09.2026 22:42
    /// kosusunda firma sifresi popup'ina hicbir karakter gitmedi ve log'da adimin
    /// tek izi "Yaz -> ***" satiriydi: metnin SURUCUYE VERILIP verilmedigi, verildiyse
    /// KAC KARAKTER oldugu okunamiyordu. Iz satiri olmadan "deger bostu" ile "gonderildi
    /// ama ORKA yuttu" ayirt edilemez ve teshis ofiste her seferinde bastan baslar.
    ///
    /// Metnin KENDISI yazilmiyor (sifre olabilir), yalnizca uzunlugu -- o ayrim icin yetiyor.
    /// </summary>
    public static void Yaz(string metin, int? beklemeMs = null)
    {
        IzYaz(string.IsNullOrEmpty(metin)
            ? "[TUS-TESHIS] Yaz: metin BOS -- surucuye hicbir karakter verilmedi"
            : $"[TUS-TESHIS] Yaz: {metin.Length} karakter gonderiliyor");

        Surucu.MetinYaz(metin);
        Surucu.Bekle(beklemeMs ?? VarsayilanBeklemeMs);
    }

    /// <summary>
    /// 'tusAdi' tusuna 'adet' kere basar.
    ///
    /// <b>Bekleme ILK basistan once de var.</b> Onceden bekleme yalnizca her
    /// basistan SONRA konuyordu; bu, adimin ilk tusu ile pencerenin daha yeni
    /// tamamlanan aktivasyonu arasinda hicbir pay birakmiyordu ve modul ekraninda
    /// "SAG x3" istenirken ilk SAG yutuluyordu. Odak duzeltmesi one getirmeyi
    /// artik dogruluyor, bu bekleme de onun ustundeki cizim/yerlesme payi.
    /// </summary>
    public static void Tus(string tusAdi, int adet = 1, int? beklemeMs = null)
    {
        var tus = Cozumle(tusAdi);
        var bekleme = beklemeMs ?? VarsayilanBeklemeMs;

        Surucu.Bekle(bekleme);

        for (int i = 0; i < adet; i++)
        {
            IzYaz($"[TUS-TESHIS] '{tusAdi}' basis {i + 1}/{adet} -- oncesinde {bekleme} ms beklendi");
            Surucu.TusaBas(tus);
            Surucu.Bekle(bekleme);
        }
    }

    /// <summary>
    /// "CTRL+F", "CTRL+A", "ALT+F4" gibi kombinasyonlar.
    ///
    /// Tek tusluk yol da <see cref="IKlavyeSurucusu.TusaBas"/> uzerinden gidiyor:
    /// burasi eskiden dogrudan key-down gonderiyordu ve tus hic birakilmiyordu.
    /// </summary>
    public static void Kisayol(string kombinasyon, int? beklemeMs = null)
    {
        var parcalar = kombinasyon.Split('+', StringSplitOptions.RemoveEmptyEntries |
                                            StringSplitOptions.TrimEntries);
        var tuslar = parcalar.Select(Cozumle).ToArray();
        var bekleme = beklemeMs ?? VarsayilanBeklemeMs;

        Surucu.Bekle(bekleme);

        IzYaz($"[TUS-TESHIS] kisayol '{kombinasyon}' -- oncesinde {bekleme} ms beklendi");

        if (tuslar.Length == 1)
            Surucu.TusaBas(tuslar[0]);
        else
            Surucu.BirlikteBas(tuslar);

        Surucu.Bekle(bekleme);
    }

    /// <summary>Arama kutusunda mevcut metnin uzerine yazmamak icin: Ctrl+A sonra yaz.</summary>
    public static void TemizleVeYaz(string metin, int? beklemeMs = null)
    {
        Kisayol("CTRL+A", 100);
        Yaz(metin, beklemeMs);
    }

    /// <summary>
    /// Kutuyu CTRL KULLANMADAN temizler: END, SHIFT+HOME, DELETE, sonra yazar.
    ///
    /// <b>Neden var:</b> 08.09.2026 kosusunda robot 0001 yerine 1187 firmasina
    /// girdi (ORKA_1187_2026). F7 firma arama kutusu <b>otomatik tamamlama</b>
    /// alani ve icinde onceki deger duruyor; <see cref="TemizleVeYaz"/> onu
    /// CTRL+A ile secmeye calisiyor ama <b>ORKA'da CTRL kombinasyonlari
    /// calismiyor</b> (daha once ALT icin de olculmustu). Secim olmayinca yeni
    /// kod eskisinin UZERINE degil YANINA yaziliyor, otomatik tamamlama baska
    /// bir firmayi buluyor ve robot yanlis firmada calismaya devam ediyor --
    /// sessizce, cunku sonraki adimlarin hepsi "bir firma acildi" diyor.
    ///
    /// <b>Neden bu tuslar:</b> uc tus da tek basina, kombinasyonsuz calisiyor.
    /// END imleci sona goturuyor (kutu bos olsa da zararsiz), SHIFT+HOME basa
    /// kadar seciyor, DELETE secimi siliyor. SHIFT+HOME bir kombinasyon ama
    /// SHIFT bir <i>modifier</i> olarak Delphi/VCL kutularinda calisiyor;
    /// calismayan CTRL/ALT kisayollari.
    ///
    /// <b>Neden <see cref="TemizleVeYaz"/> degistirilmedi:</b> dosya secim
    /// diyalogunda (Windows'un kendi penceresi) CTRL+A calisiyor ve orada
    /// {dosyaYolu}/{hesapKodu} adimlariyla sinanmis durumda. Calisan yolu
    /// degistirmemek icin ikinci bir yol eklendi; hangi adimin hangisini
    /// kullanacagi gorev dosyasindan seciliyor (<c>Adim.Temizleme</c>).
    /// </summary>
    public static void SecVeSilVeYaz(string metin, int? beklemeMs = null)
    {
        Tus("END", 1, 100);
        Kisayol("SHIFT+HOME", 100);
        Tus("DELETE", 1, 100);
        Yaz(metin, beklemeMs);
    }

    /// <summary>
    /// Adimin sectigi temizleme yoluyla yazar.
    ///
    /// Ayri bir dagitici olmasinin sebebi: iki yol da <c>TemizleYaz</c> adimi
    /// ve motorun icinde bir <c>if</c> ile ayrilsalardi karar sinanamazdi.
    /// </summary>
    public static void TemizleVeYaz(string metin, TemizlemeYolu yol, int? beklemeMs = null)
    {
        if (yol == TemizlemeYolu.SecVeSil) SecVeSilVeYaz(metin, beklemeMs);
        else TemizleVeYaz(metin, beklemeMs);
    }

    /// <summary>
    /// Gorev dosyasindaki <c>Temizleme</c> alanini cozer. Bos/eksikse
    /// <see cref="TemizlemeYolu.CtrlA"/> -- mevcut butun adimlarin davranisi
    /// degismemeli.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Taninmayan deger. <b>Sessizce CTRL+A'ya dusulmuyor:</b> yazim hatasi
    /// ("SecVeSİl", "sec-ve-sil") tam da duzeltilmeye calisilan hatayi geri
    /// getirirdi ve gorev sessizce yanlis firmaya girerdi.
    /// </exception>
    public static TemizlemeYolu TemizlemeCoz(string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger)) return TemizlemeYolu.CtrlA;

        return deger.Trim().ToLowerInvariant() switch
        {
            "ctrla" or "ctrl+a" => TemizlemeYolu.CtrlA,
            "secvesil"          => TemizlemeYolu.SecVeSil,
            _ => throw new ArgumentException(
                $"Bilinmeyen Temizleme degeri: '{deger}'. Gecerli degerler: " +
                $"'{nameof(TemizlemeYolu.CtrlA)}' (varsayilan), '{nameof(TemizlemeYolu.SecVeSil)}'.")
        };
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
