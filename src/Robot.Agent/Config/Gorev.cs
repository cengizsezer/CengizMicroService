using System.Text.Json;
using System.Text.Json.Serialization;

namespace PkfRobot.Config;

/// <summary>
/// Bir gorev = sirali adimlar listesi.
/// Yeni is eklemek icin yeni bir JSON dosyasi yazmak yeterli, kod degismez.
/// </summary>
public class Gorev
{
    public string Ad { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public List<Adim> Adimlar { get; set; } = new();

    public static Gorev Yukle(string yol)
    {
        if (!File.Exists(yol))
            throw new FileNotFoundException($"Gorev dosyasi bulunamadi: {yol}");

        var opt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        return JsonSerializer.Deserialize<Gorev>(File.ReadAllText(yol), opt)
               ?? throw new InvalidOperationException("Gorev dosyasi okunamadi.");
    }
}

public class Adim
{
    /// <summary>
    /// Tip degerleri:
    ///   OrkaBaslat      - ORKA'yi calistir
    ///   BeklePencere    - Basligi 'Deger' iceren pencere gelene kadar bekle
    ///   Yaz             - 'Deger' metnini yaz (degisken destekler)
    ///   Tus             - 'Deger' tusuna 'Adet' kez bas (ENTER, TAB, RIGHT, ESC...)
    ///   Kisayol         - CTRL+F, CTRL+A gibi kombinasyon
    ///   Tikla           - Pencereye goreli 'X'/'Y' oranina fare ile sol tik
    ///   Bekle           - 'Sayi' milisaniye bekle
    ///   EkranGoruntusu  - Isimli ekran goruntusu al
    ///   Dogrula         - Aktif pencere basligi 'Deger' iceriyor mu, icermiyorsa hata
    ///   OnayGerekir     - DryRun ise ATLA, degilse devam (Kaydet adimlari icin)
    ///   Log             - Log dosyasina not dus
    ///
    /// BeklePencere/Dogrula/Tikla adimlarinda 'Deger' icine '|' ile birden fazla
    /// aday baslik yazilabilir: "Veri Transferi|Transfer Islemleri"
    /// </summary>
    public string Tip { get; set; } = "";

    public string Deger { get; set; } = "";
    public int Adet { get; set; } = 1;
    public int Sayi { get; set; } = 0;
    public string Not { get; set; } = "";

    /// <summary>
    /// Adet sayisini bir degiskenden al. Ornek: "modulSagOk"
    /// Komut satirindan: --degisken modulSagOk=7
    /// Degisken yoksa Adet degeri kullanilir.
    /// </summary>
    public string? AdetDegisken { get; set; }

    /// <summary>
    /// Tikla adiminda: pencerenin SOL kenarindan itibaren GENISLIGE ORANLA yatay konum.
    /// 0.0 = sol kenar, 1.0 = sag kenar. Piksel DEGIL.
    /// Oran kullanilmasinin sebebi: ekran cozunurlugu degisince piksel kayar, oran kaymaz.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Tikla adiminda: pencerenin UST kenarindan itibaren YUKSEKLIGE ORANLA dikey konum.
    /// 0.0 = ust kenar, 1.0 = alt kenar. Piksel DEGIL.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Bu adima ozel pencere bekleme suresi (saniye).
    /// Verilmezse config'deki Zamanlama.PencereTimeoutSn gecerlidir.
    /// Ornek: rapor uretimi uzun suren tek bir adim icin "TimeoutSn": 180
    /// </summary>
    public int? TimeoutSn { get; set; }
}
