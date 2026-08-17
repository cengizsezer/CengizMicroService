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

public class PencereAyar
{
    public string GirisEkrani { get; set; } = "";
    public string SubeSecim { get; set; } = "";
    public string AnaEkran { get; set; } = "ORKA_";
    public string DosyaSecim { get; set; } = "";
    public string HesapPlani { get; set; } = "";
}
