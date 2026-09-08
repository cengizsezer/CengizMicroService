using PkfRobot.Config;

namespace PkfRobot.UnitTests.Config;

/// <summary>
/// <c>AltGorev</c> = bir gorev dosyasinin adimlarini baska bir dosyaya GOMME.
///
/// <b>Neden var:</b> acilis akisi iki dosyada duruyordu (01 ORKA'yi acip modul
/// ekraninda bitiyor, 02 modul gezinmesini yapiyor) ve kuyruk yalniz TEK bir
/// acilis gorevi calistirabildigi icin 02 hic calismiyordu. Adimlari ucuncu bir
/// dosyaya kopyalamak modul gezinmesini iki yerde bakilir hale getirirdi.
///
/// <b>Neden yukleme aninda aciliyor:</b> motor <c>AltGorev</c> diye bir tip hic
/// gormesin. Acilmis liste duz bir adim dizisi oldugu icin adim sayaci, ilerleme
/// yuzdesi, hiz carpani ve kalibrasyon eskisi gibi calisiyor.
/// </summary>
public class GorevAltGorevTests : IDisposable
{
    private readonly string _klasor =
        Path.Combine(Path.GetTempPath(), "pkfrobot-altgorev-" + Guid.NewGuid().ToString("N"));

    public GorevAltGorevTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (Exception) { /* temizlik */ }
    }

    private string Yaz(string ad, string govde)
    {
        var yol = Path.Combine(_klasor, ad);
        File.WriteAllText(yol, govde);
        return yol;
    }

    private static string Gorev(params string[] adimlar)
        => "{ \"Ad\": \"t\", \"Adimlar\": [" + string.Join(",", adimlar) + "] }";

    private static string Tus(string deger) => $"{{ \"Tip\": \"Tus\", \"Deger\": \"{deger}\" }}";
    private static string Alt(string dosya) => $"{{ \"Tip\": \"AltGorev\", \"Deger\": \"{dosya}\" }}";

    [Fact]
    public void AltGorev_adimlari_yerine_gomuluyor()
    {
        Yaz("alt.json", Gorev(Tus("RIGHT"), Tus("DOWN")));
        var ust = Yaz("ust.json", Gorev(Tus("F7"), Alt("alt.json"), Tus("ENTER")));

        var gorev = PkfRobot.Config.Gorev.Yukle(ust);

        // Sira korunuyor ve AltGorev adiminin KENDISI listede kalmiyor.
        Assert.Equal(new[] { "F7", "RIGHT", "DOWN", "ENTER" },
                     gorev.Adimlar.Select(a => a.Deger));
        Assert.DoesNotContain(gorev.Adimlar, a => a.Tip.Equals("AltGorev", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ic_ice_AltGorev_aciliyor()
    {
        Yaz("en-alt.json", Gorev(Tus("DOWN")));
        Yaz("orta.json", Gorev(Alt("en-alt.json"), Tus("ENTER")));
        var ust = Yaz("ust.json", Gorev(Tus("F7"), Alt("orta.json")));

        var gorev = PkfRobot.Config.Gorev.Yukle(ust);

        Assert.Equal(new[] { "F7", "DOWN", "ENTER" }, gorev.Adimlar.Select(a => a.Deger));
    }

    [Fact]
    public void Kendini_cagiran_dosya_dongusu_patlatiyor()
    {
        // Sonsuz dongu yerine anlasilir hata: dosyayi yazan kisi neyi bozdugunu gorsun.
        var ust = Yaz("ust.json", Gorev(Alt("ust.json")));

        var hata = Assert.Throws<InvalidOperationException>(() => PkfRobot.Config.Gorev.Yukle(ust));
        Assert.Contains("dongu", hata.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dolayli_dongu_de_patlatiyor()
    {
        Yaz("a.json", Gorev(Alt("b.json")));
        Yaz("b.json", Gorev(Alt("a.json")));

        Assert.Throws<InvalidOperationException>(
            () => PkfRobot.Config.Gorev.Yukle(Path.Combine(_klasor, "a.json")));
    }

    [Fact]
    public void Bos_Deger_anlasilir_hata_veriyor()
    {
        var ust = Yaz("ust.json", Gorev("{ \"Tip\": \"AltGorev\", \"Deger\": \"\" }"));

        var hata = Assert.Throws<InvalidOperationException>(() => PkfRobot.Config.Gorev.Yukle(ust));
        Assert.Contains("AltGorev", hata.Message);
    }

    [Fact]
    public void Olmayan_dosya_adiyla_birlikte_bildiriliyor()
    {
        var ust = Yaz("ust.json", Gorev(Alt("yok.json")));

        var hata = Assert.Throws<FileNotFoundException>(() => PkfRobot.Config.Gorev.Yukle(ust));
        Assert.Contains("yok.json", hata.Message);
    }

    [Fact]
    public void AltGorev_yolu_cagiran_dosyanin_klasorune_goreli()
    {
        // gorevler klasoru publish'te oldugu gibi tasiniyor; mutlak yol yazilamaz.
        var altKlasor = Path.Combine(_klasor, "ic");
        Directory.CreateDirectory(altKlasor);
        File.WriteAllText(Path.Combine(altKlasor, "derin.json"), Gorev(Tus("ESC")));

        var ust = Yaz("ust.json", Gorev(Alt("ic/derin.json")));

        Assert.Equal(new[] { "ESC" }, PkfRobot.Config.Gorev.Yukle(ust).Adimlar.Select(a => a.Deger));
    }

    [Fact]
    public void AltGorev_olmayan_dosya_eskisi_gibi_yukleniyor()
    {
        var ust = Yaz("ust.json", Gorev(Tus("F7"), Tus("ENTER")));

        Assert.Equal(2, PkfRobot.Config.Gorev.Yukle(ust).Adimlar.Count);
    }
}
