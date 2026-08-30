using System.Text.Json;
using System.Text.Json.Serialization;

namespace PkfRobot.Config;

public class RobotConfig
{
    public string OrkaPath { get; set; } = @"C:\Orka\Orka.exe";
    public string LogKlasoru { get; set; } = @"C:\RobotLog";
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Tus/Yaz/TemizleYaz/Kisayol adimlarindan ONCE ORKA on planda mi diye bakar,
    /// degilse one getirir.
    ///
    /// Ofis testinde robot ORKA yerine cmd penceresine yazdi ve SIFRE cmd'ye gitti.
    /// Bu ayar onu engeller. Kapatmak icin ozel bir sebebin yoksa true birak.
    /// </summary>
    public bool OtomatikOneGetir { get; set; } = true;

    public GirisAyar Giris { get; set; } = new();
    public FirmaAyar Firma { get; set; } = new();
    public ZamanlamaAyar Zamanlama { get; set; } = new();
    public EkranGoruntusuAyar EkranGoruntusu { get; set; } = new();
    public PencereAyar Pencereler { get; set; } = new();
    public List<string> BeklenmeyenPencereler { get; set; } = new();

    /// <summary>Hub baglantisi (--ajan modu). ORKA otomasyonundan bagimsiz.</summary>
    public AjanAyar Ajan { get; set; } = new();

    public static RobotConfig Yukle(string yol)
    {
        if (!File.Exists(yol))
            throw new FileNotFoundException($"Ayar dosyasi bulunamadi: {yol}");

        var json = File.ReadAllText(yol);
        var opt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var cfg = JsonSerializer.Deserialize<RobotConfig>(json, opt)
                  ?? throw new InvalidOperationException("Ayar dosyasi okunamadi.");
        return cfg;
    }
}

public class GirisAyar
{
    public string Veritabani { get; set; } = "";
    public string Kullanici { get; set; } = "";
    public string Sifre { get; set; } = "";

    /// <summary>
    /// ORKA ikinci kez sifre sorar (firma acilirken). Goreve {firmaSifre} olarak gecer.
    /// Oncelik: --degisken firmaSifre=xxx > ORKA_FIRMA_SIFRE ortam degiskeni > bu alan.
    /// </summary>
    public string FirmaSifresi { get; set; } = "";
}

public class FirmaAyar
{
    public string Kod { get; set; } = "";
    public string BaslikDogrulama { get; set; } = "";
}

public class ZamanlamaAyar
{
    public int AdimBeklemeMs { get; set; } = 700;
    public int TusBeklemeMs { get; set; } = 150;
    public int PencereTimeoutSn { get; set; } = 40;
    public int OrkaAcilisTimeoutSn { get; set; } = 90;
}

public class EkranGoruntusuAyar
{
    public bool HerAdimda { get; set; } = true;
    public bool HataDurumunda { get; set; } = true;
}

/// <summary>
/// Ajanin sunucuya baglanma ayarlari.
///
/// Adresler koda gomulmedi: yayin adresi degistiginde ya da bir seyi yerelde
/// denemek gerektiginde Notepad yetsin, yeniden derleme gerekmesin -- projedeki
/// appsettings disiplininin ayni gerekcesi.
/// </summary>
public class AjanAyar
{
    /// <summary>Anahtari token'a ceviren uc.</summary>
    public string TokenUcu { get; set; } = "https://www.dijitalmasraf.com/auth/agent/token";

    /// <summary>Hub adresi. wss:// yazilabilir; istemci https://'e cevirir.</summary>
    public string HubAdresi { get; set; } = "https://www.dijitalmasraf.com/agenthub";

    /// <summary>
    /// Kalp atisi araligi. Sunucunun zaman asimi 90 saniye; 30 saniye, tek bir
    /// kacan atisin ajani listeden dusurmemesi icin secildi.
    /// </summary>
    public int KalpAtisiSaniye { get; set; } = 30;

    /// <summary>Token'in kalan omru bunun altina inince yenilenir.</summary>
    public int TokenYenilemeEsigiDakika { get; set; } = 30;

    /// <summary>Ajan log dosyalari bu gunden eskiyse silinir.</summary>
    public int LogSaklamaGun { get; set; } = 14;

    /// <summary>ORKA'nin surec adi (uzantisiz). OrkaPath ile ayni exe olmali.</summary>
    public string OrkaSurecAdi { get; set; } = "OrkaWinIceberg.64";

    /// <summary>
    /// Is dosyalarinin indirildigi kok (<c>{kok}/is/{isId}/ekstre</c> ve
    /// <c>/kod-listesi</c>). Ajan yalnizca kendi isinin dosyalarini alabiliyor.
    /// </summary>
    public string IsUcuKoku { get; set; } = "https://www.dijitalmasraf.com/catalog/agent";

    /// <summary>Hata ekraninin yuklendigi uc (FileApiService, genel yukleme).</summary>
    public string DosyaYuklemeUcu { get; set; } = "https://www.dijitalmasraf.com/file/v1/uploads";
}

public class PencereAyar
{
    public string GirisEkrani { get; set; } = "";
    public string SubeSecim { get; set; } = "";
    public string AnaEkran { get; set; } = "ORKA_";
    public string DosyaSecim { get; set; } = "";
    public string HesapPlani { get; set; } = "";
}
