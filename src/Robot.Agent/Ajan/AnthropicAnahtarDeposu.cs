using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace PkfRobot.Ajan;

/// <summary>
/// Goruntu dogrulamasinin API anahtarini diskte tutan yer.
///
/// <b>Neden appsettings.json degil:</b> o dosya publish ile her guncellemede
/// uzerine yaziliyor ve depoya giriyor -- anahtar orada dursa hem her yayinda
/// silinir hem de duz metin olarak surum gecmisine yazilirdi. Ajan anahtariyla
/// ayni gerekce ve ayni yer (bkz. <see cref="AjanKimlikDeposu"/>).
///
/// <b>Neden DPAPI/CurrentUser:</b> dosyayi baska bir makineye ya da baska bir
/// Windows kullanicisina kopyalayan biri okuyamaz; kopyalanan dosya ise yaramaz.
///
/// <b>Ortam degiskeni neden once:</b> gelistirme makinesinde anahtari diske hic
/// yazmadan denemek mumkun olsun. Ofiste ortam degiskeni yok, dosya okunur.
/// </summary>
[SupportedOSPlatform("windows")]
public class AnthropicAnahtarDeposu
{
    public const string DosyaAdi = "anthropic.dat";
    public const string OrtamDegiskeni = "ANTHROPIC_API_KEY";

    private readonly string _klasor;

    public AnthropicAnahtarDeposu(string? klasor = null)
    {
        _klasor = klasor ?? AjanKimlikDeposu.VarsayilanKlasor;
        Directory.CreateDirectory(_klasor);
    }

    public string Dosya => Path.Combine(_klasor, DosyaAdi);

    public bool VarMi => File.Exists(Dosya);

    /// <summary>
    /// Kullanilacak anahtar: once ortam degiskeni, sonra diskteki sifreli dosya.
    /// Ikisi de yoksa null -- cagiran taraf ozelligi sessizce kapatiyor.
    /// </summary>
    public string? Oku()
    {
        var ortam = Environment.GetEnvironmentVariable(OrtamDegiskeni);
        if (!string.IsNullOrWhiteSpace(ortam)) return ortam.Trim();

        if (!VarMi) return null;

        try
        {
            var korunan = File.ReadAllBytes(Dosya);
            var acik = ProtectedData.Unprotect(korunan, null, DataProtectionScope.CurrentUser);
            var anahtar = Encoding.UTF8.GetString(acik).Trim();
            return string.IsNullOrWhiteSpace(anahtar) ? null : anahtar;
        }
        catch (CryptographicException)
        {
            // Baska kullanici, baska makine ya da bozuk dosya. Anahtar yokmus
            // gibi davraniliyor: dogrulama katmani robotu durdurmamali.
            return null;
        }
    }

    public void Yaz(string hamAnahtar)
    {
        if (string.IsNullOrWhiteSpace(hamAnahtar))
            throw new ArgumentException("API anahtari bos olamaz.", nameof(hamAnahtar));

        var korunan = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(hamAnahtar.Trim()), null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(Dosya, korunan);
    }

    public void Sil()
    {
        if (VarMi) File.Delete(Dosya);
    }
}
