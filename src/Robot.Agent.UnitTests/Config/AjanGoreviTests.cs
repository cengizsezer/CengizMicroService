using PkfRobot.Ajan;
using PkfRobot.Config;

namespace PkfRobot.UnitTests.Config;

/// <summary>
/// Sunucudan gelen isin kostugu akis: <c>gorevler/ajan-aktar.json</c>.
///
/// <b>Neden var:</b> ajan onceden kendi <c>orkaya-aktar.json</c>'unu kosuyordu.
/// Ayni aktarim iki dosyada durunca birinde duzeltilen digerinde eksik kaldi --
/// olculdu (08.09.2026): ajan modul gezinmesinde ENTER'i BIR kez gonderiyordu,
/// 02-modul-ve-sekme.json IKI kez gonderiyor; ustelik ajan tarafindaki guard
/// 'TdxRibbon|TcxDBTreeList' VEYA oldugu icin Veri Transferi acilmadan
/// tutuyordu. Kopya silindi, ajan da kuyrugun dosyalarini kosuyor.
///
/// Asagidaki testler bu birlesmeyi dosyalarin kendisi uzerinden sabitliyor:
/// kopya geri gelirse ya da sarmalayici parcalardan birini kaybederse burada
/// patlar.
/// </summary>
public class AjanGoreviTests
{
    private static string GorevlerKlasoru()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);

        while (dizin != null)
        {
            var aday = Path.Combine(dizin.FullName, "src", "Robot.Agent", "gorevler");
            if (Directory.Exists(aday)) return aday;
            dizin = dizin.Parent;
        }

        throw new DirectoryNotFoundException("gorevler klasoru bulunamadi.");
    }

    private static string Yol(string ad) => Path.Combine(GorevlerKlasoru(), ad);

    private static List<Adim> AjanGorevi() => Gorev.Yukle(Yol(OrkayaAktarCalistirici.GorevDosyasi)).Adimlar;

    [Fact]
    public void Ajan_ayni_aktarim_govdesini_kosuyor()
    {
        // Sarmalayicinin acilmis hali, govdenin adimlarini AYNEN icermeli.
        var ajan = AjanGorevi();
        var govde = Gorev.Yukle(Yol("tek-dosya-aktar.json")).Adimlar;

        Assert.True(ajan.Count > govde.Count, "Sarmalayici acilis adimlarini icermiyor.");

        var kuyruk = ajan.TakeLast(govde.Count).ToList();

        for (var i = 0; i < govde.Count; i++)
        {
            Assert.Equal(govde[i].Tip, kuyruk[i].Tip);
            Assert.Equal(govde[i].Deger, kuyruk[i].Deger);
            Assert.Equal(govde[i].X, kuyruk[i].X, 5);
            Assert.Equal(govde[i].Y, kuyruk[i].Y, 5);
        }
    }

    [Fact]
    public void Ajan_gorevi_giris_zincirini_de_iceriyor()
    {
        // Ajan ORKA'yi kendisi aciyor: acilis parcasi dusrse is, ORKA kapaliyken
        // ilk BeklePencere'de timeout'a duserdi.
        var adimlar = AjanGorevi();

        Assert.Contains(adimlar, a => a.Tip.Equals("OrkaBaslat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(adimlar, a => a.Deger.Contains("{firmaKodu}", StringComparison.Ordinal));
        Assert.Contains(adimlar, a => a.Deger.Contains("{sifre}", StringComparison.Ordinal));
        Assert.Contains(adimlar, a => a.Deger.Contains("{firmaSifre}", StringComparison.Ordinal));

        // Ve govdenin degiskenleri.
        Assert.Contains(adimlar, a => a.Deger.Contains("{dosyaYolu}", StringComparison.Ordinal));
        Assert.Contains(adimlar, a => a.Deger.Contains("{hesapKodu}", StringComparison.Ordinal));
        Assert.Contains(adimlar, a => a.Tip.Equals("GridDoldur", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ajan_gorevi_adim_KOPYALAMIYOR()
    {
        // Dosyanin kendisi yalniz referans tutmali; adim kopyalanirsa ayrisma
        // bastan basliyor demektir.
        var ham = File.ReadAllText(Yol(OrkayaAktarCalistirici.GorevDosyasi));
        var dosya = System.Text.Json.JsonDocument.Parse(ham, new System.Text.Json.JsonDocumentOptions
        {
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var tipler = dosya.RootElement.GetProperty("Adimlar").EnumerateArray()
                          .Select(a => a.GetProperty("Tip").GetString())
                          .ToList();

        Assert.All(tipler, t => Assert.Equal("AltGorev", t));
        Assert.Equal(2, tipler.Count);
    }

    [Fact]
    public void Ayri_ajan_gorevi_dosyasi_geri_gelmedi()
    {
        // Kopyanin geri eklenmesi, birlestirmeyi sessizce geri almanin en kolay
        // yolu olurdu.
        Assert.False(File.Exists(Yol("orkaya-aktar.json")),
                     "orkaya-aktar.json geri gelmis: aktarim yine iki yerde bakim istiyor.");
    }

    [Fact]
    public void Ajan_gorevi_publish_ciktisinda_var()
    {
        // csproj 'gorevler\\*.json' jokeriyle kopyaliyor; dosya adi degisirse
        // ajan calisma aninda "gorev dosyasi bulunamadi" der.
        var yol = Path.Combine(AppContext.BaseDirectory, "gorevler", OrkayaAktarCalistirici.GorevDosyasi);

        Assert.True(File.Exists(yol), $"Gorev dosyasi cikti klasorunde yok: {yol}");
    }
}
