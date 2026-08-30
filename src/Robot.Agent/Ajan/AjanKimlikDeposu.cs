using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace PkfRobot.Ajan;

/// <summary>
/// Ajanin kimligini diskte tutan yer: sunucudan alinan anahtar ve makineye ozgu
/// kararli kimlik.
///
/// <b>Neden publish klasoru degil, %AppData%:</b> publish klasoru her guncellemede
/// uzerine yaziliyor. Anahtar orada dursa her guncellemede kaybolur ve ofiste
/// yeniden girilmesi gerekirdi -- <c>appsettings.json</c> disiplininin sebebi de
/// bu (bkz. OKUBENI).
///
/// <b>Neden DPAPI:</b> anahtar diskte duz metin durmasin. <c>CurrentUser</c>
/// kapsami, dosyayi baska bir makineye ya da baska bir Windows kullanicisina
/// kopyalayan birinin okuyamamasi demek -- kopyalanan dosya ise yaramaz.
/// </summary>
[SupportedOSPlatform("windows")]
public class AjanKimlikDeposu
{
    public const string AnahtarDosyaAdi = "agent.dat";
    public const string MakineDosyaAdi = "makine.dat";

    private readonly string _klasor;

    public AjanKimlikDeposu(string klasor)
    {
        _klasor = klasor;
        Directory.CreateDirectory(_klasor);
    }

    /// <summary>Varsayilan yer: <c>%AppData%\PkfRobot</c>.</summary>
    public static string VarsayilanKlasor => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PkfRobot");

    public string AnahtarDosyasi => Path.Combine(_klasor, AnahtarDosyaAdi);
    public string MakineDosyasi => Path.Combine(_klasor, MakineDosyaAdi);

    public bool AnahtarVarMi => File.Exists(AnahtarDosyasi);

    /// <summary>
    /// Kayitli anahtar. Dosya yoksa ya da cozulemiyorsa (baska kullanici, baska
    /// makine, bozuk dosya) null doner -- cagiran taraf yeniden sorar.
    /// </summary>
    public string? AnahtarOku()
    {
        if (!AnahtarVarMi) return null;

        try
        {
            var korunan = File.ReadAllBytes(AnahtarDosyasi);
            var acik = ProtectedData.Unprotect(korunan, null, DataProtectionScope.CurrentUser);
            var anahtar = Encoding.UTF8.GetString(acik).Trim();
            return string.IsNullOrWhiteSpace(anahtar) ? null : anahtar;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void AnahtarYaz(string hamAnahtar)
    {
        if (string.IsNullOrWhiteSpace(hamAnahtar))
            throw new ArgumentException("Ajan anahtari bos olamaz.", nameof(hamAnahtar));

        var korunan = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(hamAnahtar.Trim()), null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(AnahtarDosyasi, korunan);
    }

    public void AnahtarSil()
    {
        if (AnahtarVarMi) File.Delete(AnahtarDosyasi);
    }

    /// <summary>
    /// Makineye ozgu, calistirmalar arasinda <b>degismeyen</b> kimlik:
    /// makine adi + ilk calistirmada uretilip saklanan GUID.
    ///
    /// Her acilista yeni bir kimlik uretilseydi sunucudaki listede ayni
    /// makineden hayalet kayitlar birikirdi (eski kayit ancak kalp atisi
    /// zaman asimiyla dusuyor). Makine adi tek basina yetmiyor: iki ofiste ayni
    /// ada sahip iki PC olabilir.
    /// </summary>
    public string MakineId()
    {
        var guid = GuidOku() ?? GuidUret();
        return $"{Environment.MachineName}-{guid}";
    }

    private string? GuidOku()
    {
        if (!File.Exists(MakineDosyasi)) return null;

        var icerik = File.ReadAllText(MakineDosyasi).Trim();
        return Guid.TryParse(icerik, out var g) ? g.ToString("N") : null;
    }

    private string GuidUret()
    {
        var guid = Guid.NewGuid();
        File.WriteAllText(MakineDosyasi, guid.ToString("D"));
        return guid.ToString("N");
    }
}
