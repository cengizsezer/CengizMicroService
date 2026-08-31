using System.Text.Json;
using System.Text.Json.Serialization;

namespace PkfRobot.Ayarlar;

/// <summary>Yedek dosyasinin govdesi. Basligi olan bir zarf, ciplak ayar degil.</summary>
public class AyarYedegi
{
    /// <summary>Bicim surumu; ileride alan eklenirse eski yedegi taniyabilmek icin.</summary>
    public int Surum { get; set; } = 1;

    public DateTime Alindi { get; set; } = DateTime.Now;
    public string Makine { get; set; } = Environment.MachineName;

    /// <summary>
    /// Yedekte <b>sifre yok</b>. DPAPI ile sifrelenen bir deger zaten baska
    /// makinede cozulemez; yedege duz metin koymak ise "makine degistirmek"
    /// icin sifreyi bir dosyaya dokmek olurdu.
    /// </summary>
    public RobotAyarlari Ayarlar { get; set; } = new();
}

/// <summary>
/// Ayarlarin diskteki yeri: <c>%AppData%\PkfRobot\ayarlar.json</c>.
///
/// <b>Neden publish klasoru degil:</b> publish klasoru her guncellemede uzerine
/// yaziliyor. Ofiste test edilip duzeltilmis ayarlar orada dursa her yayinda
/// silinirdi -- ajan anahtarinin <c>%AppData%</c>'da durmasinin sebebi de bu
/// (bkz. <see cref="PkfRobot.Ajan.AjanKimlikDeposu"/>).
/// </summary>
public class AyarDeposu
{
    public const string DosyaAdi = "ayarlar.json";

    private static readonly JsonSerializerOptions Bicim = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions Okuma = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _klasor;

    public AyarDeposu(string klasor)
    {
        _klasor = klasor;
        Directory.CreateDirectory(_klasor);
    }

    /// <summary>Varsayilan yer: <c>%AppData%\PkfRobot</c> -- ajan anahtariyla ayni kok.</summary>
    public static string VarsayilanKlasor => PkfRobot.Ajan.AjanKimlikDeposu.VarsayilanKlasor;

    public string Dosya => Path.Combine(_klasor, DosyaAdi);

    public bool VarMi => File.Exists(Dosya);

    /// <summary>
    /// Kayitli ayarlar. Dosya yoksa ya da bozuksa <b>bos ayar</b> doner:
    /// bozuk bir JSON yuzunden arayuzun hic acilmamasi, kullanicinin ayarlari
    /// duzeltebilecegi tek yeri de kapatmak olurdu.
    /// </summary>
    public RobotAyarlari Oku()
    {
        if (!VarMi) return new RobotAyarlari();

        try
        {
            return JsonSerializer.Deserialize<RobotAyarlari>(File.ReadAllText(Dosya), Okuma)
                   ?? new RobotAyarlari();
        }
        catch (JsonException)
        {
            return new RobotAyarlari();
        }
    }

    /// <summary>
    /// Ayarlari yazar. Once gecici dosyaya, sonra yerine tasiyarak: yazma
    /// sirasinda uygulama olurse yarim bir ayarlar.json kalmasin.
    /// </summary>
    public void Yaz(RobotAyarlari ayarlar)
    {
        var gecici = Dosya + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(ayarlar, Bicim));

        if (File.Exists(Dosya)) File.Replace(gecici, Dosya, null);
        else File.Move(gecici, Dosya);
    }

    /// <summary>Ayarlari tek dosyaya cikarir. Sifreler dahil edilmez.</summary>
    public void Yedekle(string hedefDosya)
    {
        var yedek = new AyarYedegi { Ayarlar = Oku() };

        var klasor = Path.GetDirectoryName(hedefDosya);
        if (!string.IsNullOrEmpty(klasor)) Directory.CreateDirectory(klasor);

        File.WriteAllText(hedefDosya, JsonSerializer.Serialize(yedek, Bicim));
    }

    /// <summary>
    /// Yedegi geri yukler ve yuklenen ayarlari dondurur.
    ///
    /// Ham <c>ayarlar.json</c> de kabul ediliyor: elle kopyalanmis bir dosyayi
    /// "yedek degil" diye reddetmek, kullaniciyi dosyayi elle tasimaya iter.
    /// </summary>
    public RobotAyarlari GeriYukle(string kaynakDosya)
    {
        if (!File.Exists(kaynakDosya))
            throw new FileNotFoundException($"Yedek dosyasi bulunamadi: {kaynakDosya}");

        var metin = File.ReadAllText(kaynakDosya);

        RobotAyarlari? ayarlar;
        try
        {
            ayarlar = JsonSerializer.Deserialize<AyarYedegi>(metin, Okuma)?.Ayarlar;

            // Zarf degil, dogrudan ayar dosyasi verilmis olabilir.
            if (ayarlar is null || BosMu(ayarlar))
                ayarlar = JsonSerializer.Deserialize<RobotAyarlari>(metin, Okuma);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Yedek dosyasi okunamadi: {ex.Message}", ex);
        }

        if (ayarlar is null)
            throw new InvalidDataException("Yedek dosyasi bos ya da taninmayan bicimde.");

        Yaz(ayarlar);
        return ayarlar;
    }

    private static bool BosMu(RobotAyarlari a)
        => a.Koordinatlar.Count == 0
           && string.IsNullOrEmpty(a.OrkaExeYolu)
           && string.IsNullOrEmpty(a.FirmaKodu)
           && string.IsNullOrEmpty(a.KullaniciKodu)
           && string.IsNullOrEmpty(a.LogKlasoru)
           && string.IsNullOrEmpty(a.IsDosyalariKlasoru);
}
