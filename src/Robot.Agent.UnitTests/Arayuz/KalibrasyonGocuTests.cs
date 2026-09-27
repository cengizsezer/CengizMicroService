using PkfRobot.Ayarlar;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Silinen <c>orkaya-aktar.json</c>'a bakan kalibrasyon kayitlarinin gocu.
///
/// <b>Neden onemli:</b> olcumler gorev dosyasinda degil
/// <c>%AppData%\PkfRobot\ayarlar.json</c> icinde duruyor. Goc olmasaydi ofiste
/// olculmus oranlar bir daha hicbir dosyaya uygulanmaz, uzerine her acilista
/// "AdimYok" uyarisi cikardi -- ve kimse bunu silinen dosyayla iliskilendirmezdi.
/// </summary>
public class KalibrasyonGocuTests
{
    [Fact]
    public void Eski_anahtar_yeni_dosyaya_tasiniyor()
    {
        // Iki dosyanin ilk dort Tikla adimi ayni sirada ve ayni oranlardaydi
        // (bir test onlari esit tutuyordu), o yuzden #N -> #N birebir.
        var ayarlar = new RobotAyarlari();
        ayarlar.KoordinatYaz("orkaya-aktar.json#2", "Transfere Basla", 0.5, 0.12);

        var satirlar = KalibrasyonGocu.Uygula(ayarlar);

        var kayit = Assert.Single(ayarlar.Koordinatlar);
        Assert.Equal("tek-dosya-aktar.json#2", kayit.Anahtar);
        Assert.Equal(0.5, kayit.X, 5);
        Assert.Equal(0.12, kayit.Y, 5);
        Assert.Contains(satirlar, s => s.Contains("tasindi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Yeni_anahtarda_olcum_varsa_eski_kayit_siliniyor()
    {
        // Yeni dosyanin kendi olcumu daha guncel: ustune yazmak, ofiste yapilan
        // son olcumu geri almak olurdu.
        var ayarlar = new RobotAyarlari();
        ayarlar.KoordinatYaz("tek-dosya-aktar.json#0", "Sol panel - Banka Ekstresi", 0.125, 0.30);
        ayarlar.KoordinatYaz("orkaya-aktar.json#0", "Sol panel - Banka Ekstresi", 0.081, 0.32);

        KalibrasyonGocu.Uygula(ayarlar);

        var kayit = Assert.Single(ayarlar.Koordinatlar);
        Assert.Equal("tek-dosya-aktar.json#0", kayit.Anahtar);
        Assert.Equal(0.125, kayit.X, 5);
    }

    [Fact]
    public void Diger_dosyalarin_kayitlarina_dokunmuyor()
    {
        var ayarlar = new RobotAyarlari();
        ayarlar.KoordinatYaz("tek-dosya-aktar.json#4", "Sekme kapatma carpisi", 0.95, 0.06);
        ayarlar.KoordinatYaz("03-banka-transferi.json#0", "Transfere Basla", 0.5, 0.12);

        var satirlar = KalibrasyonGocu.Uygula(ayarlar);

        Assert.Empty(satirlar);
        Assert.Equal(2, ayarlar.Koordinatlar.Count);
    }

    [Fact]
    public void Ikinci_calistirmada_tasinacak_kayit_kalmiyor()
    {
        // Goc her acilista kosuyor; ikinci kez calisinca hicbir sey yapmamali
        // ki ayarlar dosyasi bos yere yeniden yazilmasin.
        var ayarlar = new RobotAyarlari();
        ayarlar.KoordinatYaz("orkaya-aktar.json#1", "Sablon satiri", 0.4, 0.25);

        Assert.NotEmpty(KalibrasyonGocu.Uygula(ayarlar));
        Assert.Empty(KalibrasyonGocu.Uygula(ayarlar));
    }

    [Fact]
    public void Tasinan_kayit_gorev_dosyasina_uygulanabiliyor()
    {
        // Gocun asil sinavi: tasinan anahtar, yeni dosyadaki adimi GERCEKTEN
        // buluyor mu? Not eslesmesi de sart -- KalibrasyonUygulama aciklamasi
        // degismis bir adima korlemesine yazmiyor.
        var klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-goc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);

        try
        {
            File.WriteAllText(Path.Combine(klasor, "tek-dosya-aktar.json"), """
            {
              "Ad": "Dongu govdesi",
              "Adimlar": [
                { "Tip": "Tikla", "X": 0.125, "Y": 0.3, "Not": "Sol panel - Banka Ekstresi" }
              ]
            }
            """);

            var ayarlar = new RobotAyarlari();
            ayarlar.KoordinatYaz("orkaya-aktar.json#0", "Sol panel - Banka Ekstresi", 0.081, 0.32);

            KalibrasyonGocu.Uygula(ayarlar);
            var rapor = KalibrasyonUygulama.Uygula(klasor, ayarlar.Koordinatlar);

            Assert.False(rapor.SorunVar);
            Assert.Equal(1, rapor.Uygulanan);
        }
        finally
        {
            try { Directory.Delete(klasor, recursive: true); } catch (IOException) { }
        }
    }
}
