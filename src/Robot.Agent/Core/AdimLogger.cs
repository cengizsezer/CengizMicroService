using System.Text;
using FlaUI.Core.Capturing;

namespace PkfRobot.Core;

/// <summary>
/// Her calistirma icin ayri klasor acar, adim adim log + ekran goruntusu yazar.
/// Ofiste bir sey ters giderse bu klasoru zip'leyip getirmek yeterli olmali.
/// </summary>
public class AdimLogger : IDisposable
{
    private readonly string _klasor;
    private readonly StreamWriter _log;
    private int _adimNo;
    private readonly bool _ekranGoruntusuAktif;

    public string Klasor => _klasor;

    public AdimLogger(string kokKlasor, string gorevAdi, bool ekranGoruntusuAktif)
    {
        _ekranGoruntusuAktif = ekranGoruntusuAktif;
        var zaman = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        _klasor = Path.Combine(kokKlasor, $"{zaman}_{Temizle(gorevAdi)}");
        Directory.CreateDirectory(_klasor);

        _log = new StreamWriter(Path.Combine(_klasor, "log.txt"), append: true, Encoding.UTF8)
        {
            AutoFlush = true
        };

        Bilgi($"=== {gorevAdi} basladi ===");
        Bilgi($"Makine: {Environment.MachineName}  Kullanici: {Environment.UserName}");
    }

    public void Bilgi(string mesaj) => Yaz("BILGI", mesaj);
    public void Uyari(string mesaj) => Yaz("UYARI", mesaj);
    public void Hata(string mesaj) => Yaz("HATA ", mesaj);

    public void Adim(string tip, string detay)
    {
        _adimNo++;
        Yaz("ADIM ", $"[{_adimNo:D2}] {tip} -> {detay}");
    }

    private void Yaz(string seviye, string mesaj)
    {
        var satir = $"{DateTime.Now:HH:mm:ss.fff} [{seviye}] {mesaj}";
        _log.WriteLine(satir);
        Console.WriteLine(satir);
    }

    /// <summary>Tum ekranin goruntusunu alir. Hata olursa sessizce gecer.</summary>
    public void EkranAl(string ad, bool zorla = false)
    {
        if (!_ekranGoruntusuAktif && !zorla) return;

        try
        {
            var dosya = Path.Combine(_klasor, $"adim-{_adimNo:D2}-{Temizle(ad)}.png");
            Capture.Screen().ToFile(dosya);
        }
        catch (Exception ex)
        {
            Uyari($"Ekran goruntusu alinamadi: {ex.Message}");
        }
    }

    private static string Temizle(string s)
    {
        var gecersiz = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in s)
            sb.Append(gecersiz.Contains(c) || c == ' ' ? '-' : c);
        return sb.ToString().ToLowerInvariant();
    }

    public void Dispose()
    {
        Bilgi("=== bitti ===");
        _log.Dispose();
    }
}
