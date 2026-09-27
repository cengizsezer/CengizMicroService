using PkfRobot.Ayarlar;
using PkfRobot.Config;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Cift tik, ORKA'nin dayattigi bir kural: Banka Ekstreleri grid'indeki sablon
/// satirina GIRMEK icin cift tik gerekiyor -- tek tik satiri yalnizca secili
/// yapiyor, Enter da acmiyor. Zincir ofiste tam bu adimda duruyordu.
///
/// Fare hareketi test edilemez ama <b>kural</b> edilebilir: bayrak varsayilan
/// olarak kapali mi, gorev dosyasindan okunuyor mu ve sablon satirinda acik mi.
/// </summary>
public class CiftTikTests : IDisposable
{
    private readonly string _klasor;

    public CiftTikTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-cifttik-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (IOException) { }
    }

    private string GorevYaz(string ad, string icerik)
    {
        var yol = Path.Combine(_klasor, ad);
        File.WriteAllText(yol, icerik);
        return yol;
    }

    [Fact]
    public void CiftTik_yazilmayan_adim_tek_tik_kaliyor()
    {
        // Varsayilanin false olmasi sart: mevcut butun Tikla adimlari tek tik ve
        // bir alan eklendi diye hepsinin davranisi degismemeli.
        var yol = GorevYaz("tek-tik.json", """
        {
          "Ad": "Ornek Gorev",
          "Adimlar": [
            { "Tip": "Tikla", "X": 0.125, "Y": 0.3, "Not": "Sol panel" }
          ]
        }
        """);

        var gorev = Gorev.Yukle(yol);

        Assert.False(gorev.Adimlar[0].CiftTik);
    }

    [Fact]
    public void CiftTik_gorev_dosyasindan_okunuyor()
    {
        var yol = GorevYaz("cift-tik.json", """
        {
          "Ad": "Ornek Gorev",
          "Adimlar": [
            { "Tip": "Tikla", "X": 0.4, "Y": 0.25, "CiftTik": true, "Not": "Sablon satiri" },
            { "Tip": "Tikla", "X": 0.5, "Y": 0.12, "CiftTik": false, "Not": "Transfere Basla" }
          ]
        }
        """);

        var gorev = Gorev.Yukle(yol);

        Assert.True(gorev.Adimlar[0].CiftTik);
        Assert.False(gorev.Adimlar[1].CiftTik);
    }

    [Fact]
    public void Sablon_satiri_cift_tikla_aciliyor_digerleri_tek_tik()
    {
        // Kural gorev dosyasinda durdugu icin dosyanin kendisi uzerinden
        // sabitleniyor: sablon satiri cift tik, geri kalan her Tikla tek tik.
        //
        // Ajanin kostugu AKISIN TAMAMI okunuyor (acilis + govde): kural yalniz
        // govdede degil, birlesmis zincirde de tutmali.
        var yol = Path.Combine(AppContext.BaseDirectory, "gorevler", "ajan-aktar.json");
        Assert.True(File.Exists(yol), $"Gorev dosyasi publish ciktisinda yok: {yol}");

        var tiklaAdimlari = Gorev.Yukle(yol).Adimlar
            .Where(a => a.Tip.Equals("Tikla", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var ciftTiklananlar = tiklaAdimlari.Where(a => a.CiftTik).ToList();

        Assert.Single(ciftTiklananlar);
        Assert.Contains("Sablon satiri", ciftTiklananlar[0].Not, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Kalibrasyon_koordinati_yazarken_cift_tik_bayragini_dusurmuyor()
    {
        // Kalibrasyon gorev JSON'unu yeniden yaziyor. Bayrak orada kaybolsaydi
        // zincir bir sonraki kalibrasyondan sonra sessizce yine sablon satirinda
        // dururdu -- ve kimse koordinat degisikligini suclu sanmazdi.
        var yol = GorevYaz("tek-dosya-aktar.json", """
        {
          "Ad": "Ornek Gorev",
          "Adimlar": [
            { "Tip": "Tikla", "X": 0.4, "Y": 0.25, "CiftTik": true, "Not": "Sablon satiri" }
          ]
        }
        """);

        KalibrasyonUygulama.Uygula(_klasor, new[]
        {
            new KoordinatAyari { Anahtar = "tek-dosya-aktar.json#0", Not = "Sablon satiri", X = 0.42, Y = 0.27 }
        });

        var adim = Gorev.Yukle(yol).Adimlar[0];

        Assert.True(adim.CiftTik);
        Assert.Equal(0.42, adim.X, 3);
    }
}
