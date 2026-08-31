using System.Text.Json;
using System.Text.Json.Nodes;

namespace PkfRobot.Ayarlar;

/// <summary>
/// Arayuzde bir satir olarak gorunen koordinat.
/// </summary>
/// <param name="Anahtar">Ayar dosyasinda bu koordinati bulan anahtar.</param>
/// <param name="GorevDosyasi">Koordinatin gectigi gorev JSON dosyasinin tam yolu.</param>
/// <param name="GorevAdi">Gorevin okunabilir adi.</param>
/// <param name="TiklaSirasi">Dosyadaki kacinci <c>Tikla</c> adimi (0'dan).</param>
/// <param name="Aciklama">Adimin <c>Not</c> alani: "Sol panel - Banka Ekstresi".</param>
/// <param name="HedefPencere">Adimin <c>Deger</c> alani; bossa config'deki ana ekran.</param>
public record KoordinatKaydi(
    string Anahtar,
    string GorevDosyasi,
    string GorevAdi,
    int TiklaSirasi,
    string Aciklama,
    string HedefPencere,
    double X,
    double Y)
{
    public string DosyaAdi => Path.GetFileName(GorevDosyasi);

    /// <summary>Aciklamasi olmayan koordinat alti ay sonra anlasilmaz; bunu goster.</summary>
    public string Etiket => string.IsNullOrWhiteSpace(Aciklama)
        ? $"{DosyaAdi} · {TiklaSirasi + 1}. Tikla adimi (aciklama yok)"
        : Aciklama;
}

/// <summary>
/// Koordinat satirlari <b>gorev JSON dosyalarindan turetilir</b>, elle yazilmaz.
///
/// Sebep: liste kodda dursaydi yeni bir gorev dosyasi eklendiginde ya da bir
/// <c>Tikla</c> adimi cikarildiginda arayuz gercekle ayrisirdi -- ve ayrildigini
/// kimse fark etmezdi. Gorev dosyalari akisin tek kaynagi; arayuz onlari okuyor.
/// </summary>
public static class KoordinatKesfi
{
    public const string TiklaTipi = "Tikla";

    private static readonly JsonDocumentOptions Okuma = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Ayar dosyasindaki anahtar: <c>orkaya-aktar.json#0</c>.
    ///
    /// Adim <b>indeksi</b> degil <c>Tikla</c> <b>sirasi</b> kullaniliyor: goreve
    /// araya bir Bekle ya da EkranGoruntusu adimi eklemek koordinat
    /// kalibrasyonunu bozmasin.
    /// </summary>
    public static string Anahtar(string gorevDosyaAdi, int tiklaSirasi)
        => $"{gorevDosyaAdi}#{tiklaSirasi}";

    /// <summary>Klasordeki butun gorev dosyalarinin Tikla adimlari.</summary>
    public static List<KoordinatKaydi> Kesfet(string gorevlerKlasoru)
    {
        var sonuc = new List<KoordinatKaydi>();
        if (!Directory.Exists(gorevlerKlasoru)) return sonuc;

        foreach (var dosya in Directory.GetFiles(gorevlerKlasoru, "*.json").OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            sonuc.AddRange(KesfetDosya(dosya));

        return sonuc;
    }

    /// <summary>
    /// Tek dosyanin Tikla adimlari. Bozuk bir JSON butun listeyi dusurmesin diye
    /// okunamayan dosya sessizce atlaniyor -- arayuz calismaya devam etmeli.
    /// </summary>
    public static List<KoordinatKaydi> KesfetDosya(string gorevDosyasi)
    {
        var sonuc = new List<KoordinatKaydi>();

        JsonNode? kok;
        try
        {
            kok = JsonNode.Parse(File.ReadAllText(gorevDosyasi), documentOptions: Okuma);
        }
        catch (Exception)
        {
            return sonuc;
        }

        if (kok is not JsonObject gorev || gorev["Adimlar"] is not JsonArray adimlar)
            return sonuc;

        var gorevAdi = Metin(gorev, "Ad");
        var dosyaAdi = Path.GetFileName(gorevDosyasi);
        var tiklaSirasi = 0;

        foreach (var dugum in adimlar)
        {
            if (dugum is not JsonObject adim) continue;
            if (!TiklaMi(adim)) continue;

            sonuc.Add(new KoordinatKaydi(
                Anahtar(dosyaAdi, tiklaSirasi),
                gorevDosyasi,
                gorevAdi,
                tiklaSirasi,
                Metin(adim, "Not"),
                Metin(adim, "Deger"),
                Sayi(adim, "X"),
                Sayi(adim, "Y")));

            tiklaSirasi++;
        }

        return sonuc;
    }

    internal static bool TiklaMi(JsonObject adim)
        => string.Equals(Metin(adim, "Tip"), TiklaTipi, StringComparison.OrdinalIgnoreCase);

    internal static string Metin(JsonObject nesne, string alan)
    {
        // Alan adlari JSON'da buyuk harfle yazili ama motor buyuk/kucuk harfe
        // duyarsiz okuyor; arayuz de ayni sekilde davranmali.
        var deger = Alan(nesne, alan);
        return deger is null ? string.Empty : deger.GetValue<JsonElement>().ValueKind == JsonValueKind.String
            ? deger.GetValue<string>()
            : string.Empty;
    }

    internal static double Sayi(JsonObject nesne, string alan)
    {
        var deger = Alan(nesne, alan);
        if (deger is null) return 0;

        var eleman = deger.GetValue<JsonElement>();
        return eleman.ValueKind == JsonValueKind.Number && eleman.TryGetDouble(out var d) ? d : 0;
    }

    internal static JsonValue? Alan(JsonObject nesne, string alan)
    {
        foreach (var (ad, deger) in nesne)
        {
            if (!string.Equals(ad, alan, StringComparison.OrdinalIgnoreCase)) continue;
            return deger as JsonValue;
        }
        return null;
    }
}
