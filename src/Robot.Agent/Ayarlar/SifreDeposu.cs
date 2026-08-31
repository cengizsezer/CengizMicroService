using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PkfRobot.Ayarlar;

/// <summary>ORKA giris sifreleri. Diskte asla duz metin durmuyor.</summary>
public class Sifreler
{
    /// <summary>ORKA kullanici sifresi.</summary>
    public string OrkaSifresi { get; set; } = string.Empty;

    /// <summary>ORKA firma acarken ikinci kez sordugu sifre.</summary>
    public string FirmaSifresi { get; set; } = string.Empty;

    public bool BosMu => string.IsNullOrEmpty(OrkaSifresi) && string.IsNullOrEmpty(FirmaSifresi);
}

/// <summary>
/// Sifrelerin diskteki yeri: <c>%AppData%\PkfRobot\sifreler.dat</c>, DPAPI ile
/// sifreli.
///
/// <b>Neden ayarlar.json'da degil:</b> ayarlar.json duz metin ve yedeklenip
/// baska makineye tasinabiliyor. Sifre orada dursa yedek dosyasi bir sifre
/// listesine donerdi. Ayri dosya + <c>CurrentUser</c> kapsami: dosyayi baska bir
/// makineye ya da baska bir Windows kullanicisina kopyalayan onu cozemez --
/// ajan anahtarindaki kalibin ayni (bkz. <see cref="PkfRobot.Ajan.AjanKimlikDeposu"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public class SifreDeposu
{
    public const string DosyaAdi = "sifreler.dat";

    private readonly string _klasor;

    public SifreDeposu(string klasor)
    {
        _klasor = klasor;
        Directory.CreateDirectory(_klasor);
    }

    public string Dosya => Path.Combine(_klasor, DosyaAdi);

    public bool VarMi => File.Exists(Dosya);

    /// <summary>
    /// Kayitli sifreler. Dosya yoksa ya da cozulemiyorsa (baska kullanici, baska
    /// makine, bozuk dosya) bos doner -- kullanici yeniden girer.
    /// </summary>
    public Sifreler Oku()
    {
        if (!VarMi) return new Sifreler();

        try
        {
            var korunan = File.ReadAllBytes(Dosya);
            var acik = ProtectedData.Unprotect(korunan, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Sifreler>(Encoding.UTF8.GetString(acik))
                   ?? new Sifreler();
        }
        catch (CryptographicException)
        {
            return new Sifreler();
        }
        catch (JsonException)
        {
            return new Sifreler();
        }
    }

    public void Yaz(Sifreler sifreler)
    {
        var acik = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sifreler));
        var korunan = ProtectedData.Protect(acik, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Dosya, korunan);
    }

    public void Sil()
    {
        if (VarMi) File.Delete(Dosya);
    }
}
