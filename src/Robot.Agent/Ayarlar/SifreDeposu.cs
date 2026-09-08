using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PkfRobot.Ayarlar;

/// <summary>
/// ORKA giris sifreleri. Diskte asla duz metin durmuyor.
///
/// <b>Her sifre firma basina tutuluyor.</b> Ofiste tek firma varken tek alan
/// yetiyordu; birden fazla firmayla calisirken hem ORKA giris sifresi hem firma
/// sifresi firmadan firmaya degisiyor. Tek alan tutulsaydi firma degistiren
/// kullanici her seferinde ikisini de yeniden yazardi -- ve yanlis sifre ORKA'yi
/// kilitliyor.
///
/// <b>Tek alanlik eski degerler duruyor</b> (<see cref="OrkaSifresi"/>,
/// <see cref="FirmaSifresi"/>): diskte kayitli eski dosyalar okunabilsin ve firma
/// bazli kayit yokken yedek olarak kullanilabilsin diye. Oncelik sirasi
/// <see cref="FirmaSifreCozucu"/> icinde, tek yerde.
///
/// <b>Sunucuya hicbiri gonderilmiyor.</b> DijitalMasraf yalnizca firma KODUNU
/// gonderiyor; sifreler bu makinede, DPAPI ile sifreli kaliyor. Bilincli karar.
/// </summary>
public class Sifreler
{
    /// <summary>
    /// Tek alanlik ORKA kullanici sifresi. <b>Yedek:</b> firma bazli kayit
    /// yoksa kullanilir (bkz. <see cref="OrkaSifreleri"/>).
    /// </summary>
    public string OrkaSifresi { get; set; } = string.Empty;

    /// <summary>
    /// Tek alanlik firma sifresi. <b>Yedek:</b> firma bazli kayit yoksa
    /// kullanilir (bkz. <see cref="FirmaSifreleri"/>).
    /// </summary>
    public string FirmaSifresi { get; set; } = string.Empty;

    /// <summary>Firma kodu -> o firmanin ORKA GIRIS sifresi.</summary>
    public Dictionary<string, string> OrkaSifreleri { get; set; } = new();

    /// <summary>Firma kodu -> ORKA'nin firma acarken ikinci kez sordugu sifre.</summary>
    public Dictionary<string, string> FirmaSifreleri { get; set; } = new();

    /// <summary>Firmanin ORKA sifresi; kayitli degilse tek alanlik eski degere duser.</summary>
    public string OrkaSifresiAl(string? firmaKodu)
        => FirmaBazliOrkaSifresi(firmaKodu) ?? OrkaSifresi;

    /// <summary>Firmanin firma sifresi; kayitli degilse tek alanlik eski degere duser.</summary>
    public string FirmaSifresiAl(string? firmaKodu)
        => FirmaBazliFirmaSifresi(firmaKodu) ?? FirmaSifresi;

    /// <summary>
    /// YALNIZCA firma bazli kayit; yoksa null (tek alanlik yedege DUSMEZ).
    ///
    /// <see cref="FirmaSifreCozucu"/> oncelik sirasini kendisi kuruyor ve
    /// "yedege dusmus mu" ayrimini yapabilmesi icin yedeksiz bir okumaya
    /// ihtiyaci var -- log satiri hangi kaynagin kullanildigini yaziyor.
    /// </summary>
    public string? FirmaBazliOrkaSifresi(string? firmaKodu) => FirmaBazli(OrkaSifreleri, firmaKodu);

    /// <inheritdoc cref="FirmaBazliOrkaSifresi"/>
    public string? FirmaBazliFirmaSifresi(string? firmaKodu) => FirmaBazli(FirmaSifreleri, firmaKodu);

    public void OrkaSifresiYaz(string? firmaKodu, string? sifre) => Yaz(OrkaSifreleri, firmaKodu, sifre);

    public void FirmaSifresiYaz(string? firmaKodu, string? sifre) => Yaz(FirmaSifreleri, firmaKodu, sifre);

    /// <summary>Bu firma icin KENDI kaydi var mi (tek alanlik yedek sayilmaz).</summary>
    public bool FirmaKaydiVarMi(string? firmaKodu)
        => FirmaBazliOrkaSifresi(firmaKodu) is not null ||
           FirmaBazliFirmaSifresi(firmaKodu) is not null;

    /// <summary>
    /// Firma listeden cikarilinca sifreleri de gitsin.
    ///
    /// Kullanici artik calismadigi bir firmayi listeden cikardiginda sifresinin
    /// diskte durmaya devam etmesi icin bir sebep yok; "cikardim" ile "sildim"
    /// arasinda fark olmamali.
    /// </summary>
    public void FirmaKayitlariniSil(string? firmaKodu)
    {
        Sil(OrkaSifreleri, firmaKodu);
        Sil(FirmaSifreleri, firmaKodu);
    }

    public bool BosMu => string.IsNullOrEmpty(OrkaSifresi) && string.IsNullOrEmpty(FirmaSifresi)
                        && OrkaSifreleri.Values.All(string.IsNullOrEmpty)
                        && FirmaSifreleri.Values.All(string.IsNullOrEmpty);

    // Firma kodlari ORKA'da "0001" gibi sabit yazimli ama kullanici elle de
    // girebiliyor; buyuk/kucuk harf farki yuzunden kayit "yok" gorunmesin.
    private static string? FirmaBazli(Dictionary<string, string> kayitlar, string? firmaKodu)
    {
        if (string.IsNullOrWhiteSpace(firmaKodu)) return null;

        foreach (var (kod, sifre) in kayitlar)
        {
            if (string.Equals(kod, firmaKodu.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(sifre))
                return sifre;
        }

        return null;
    }

    private static void Yaz(Dictionary<string, string> kayitlar, string? firmaKodu, string? sifre)
    {
        if (string.IsNullOrWhiteSpace(firmaKodu)) return;

        Sil(kayitlar, firmaKodu);
        kayitlar[firmaKodu.Trim()] = sifre ?? string.Empty;
    }

    private static void Sil(Dictionary<string, string> kayitlar, string? firmaKodu)
    {
        if (string.IsNullOrWhiteSpace(firmaKodu)) return;

        foreach (var kod in kayitlar.Keys
                     .Where(k => string.Equals(k, firmaKodu.Trim(), StringComparison.OrdinalIgnoreCase))
                     .ToList())
            kayitlar.Remove(kod);
    }
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
