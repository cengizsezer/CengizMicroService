using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PkfRobot.Ayarlar;

public enum UygulamaDurumu
{
    /// <summary>Gorev dosyasindaki X/Y guncellendi.</summary>
    Uygulandi,

    /// <summary>Dosyadaki deger zaten kayitli degerle ayni; dosyaya dokunulmadi.</summary>
    Ayni,

    /// <summary>
    /// Adim duruyor ama aciklamasi degismis. Gorev dosyasi elden gecmis
    /// olabilir; eski olcum yeni adima korlemesine yazilmiyor.
    /// </summary>
    NotUyusmuyor,

    /// <summary>Kaydin gosterdigi Tikla adimi artik yok (ya da dosya yok).</summary>
    AdimYok
}

public record UygulamaSatiri(string Anahtar, UygulamaDurumu Durum, string Mesaj);

public record UygulamaRaporu(IReadOnlyList<UygulamaSatiri> Satirlar)
{
    public int Uygulanan => Satirlar.Count(s => s.Durum == UygulamaDurumu.Uygulandi);
    public int Ayni => Satirlar.Count(s => s.Durum == UygulamaDurumu.Ayni);

    public IEnumerable<UygulamaSatiri> Sorunlular =>
        Satirlar.Where(s => s.Durum is UygulamaDurumu.NotUyusmuyor or UygulamaDurumu.AdimYok);

    public bool SorunVar => Sorunlular.Any();
}

/// <summary>
/// Kaydedilmis kalibrasyonu gorev JSON dosyalarina yazar.
///
/// <b>Neden iki yerde duruyor:</b> adim motoru koordinati gorev dosyasindan
/// okuyor ve motora dokunulmuyor. Ama gorev dosyalari publish klasorunde ve her
/// yayinda uzerine yaziliyor -- ofiste kalibre edilen degerler orada dursa her
/// guncellemede silinirdi. Bu yuzden asil kopya
/// <c>%AppData%\PkfRobot\ayarlar.json</c> icinde; uygulama acilirken bu kopyayi
/// gorev dosyalarina geri yaziyor. Yayin sonrasi kalibrasyon kendiliginden geri
/// geliyor, elle bir sey yapilmiyor.
///
/// <b>Not kontrolu:</b> bir kayit yalniz adimin aciklamasi (Not) hala ayniysa
/// uygulaniyor. Gorev akisi degistiginde eski bir oranin yeni bir adima sessizce
/// yazilmasi, robotun yanlis yere tiklamasinin en sinsi yolu olurdu.
/// </summary>
public static class KalibrasyonUygulama
{
    private static readonly JsonDocumentOptions Okuma = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions Yazma = new()
    {
        WriteIndented = true,

        // Turkce harfler kacis dizisine donmesin; dosyayi Notepad ile duzenlemek
        // hala mumkun olmali.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static UygulamaRaporu Uygula(string gorevlerKlasoru, IEnumerable<KoordinatAyari> kayitlar)
    {
        var satirlar = new List<UygulamaSatiri>();

        // Dosya basina tek okuma-yazma: ayni dosyaya birden cok koordinat
        // dusuyorsa dosyayi her seferinde bastan acmak hem yavas hem gereksiz.
        foreach (var grup in kayitlar.GroupBy(k => DosyaAdi(k.Anahtar), StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(grup.Key))
            {
                Hepsine(satirlar, grup, UygulamaDurumu.AdimYok, "Anahtar taninmadi.");
                continue;
            }

            var yol = Path.Combine(gorevlerKlasoru, grup.Key);
            if (!File.Exists(yol))
            {
                Hepsine(satirlar, grup, UygulamaDurumu.AdimYok, $"Gorev dosyasi yok: {grup.Key}");
                continue;
            }

            JsonNode? kok;
            try
            {
                kok = JsonNode.Parse(File.ReadAllText(yol), documentOptions: Okuma);
            }
            catch (JsonException ex)
            {
                Hepsine(satirlar, grup, UygulamaDurumu.AdimYok, $"{grup.Key} okunamadi: {ex.Message}");
                continue;
            }

            if (kok is not JsonObject gorev || gorev["Adimlar"] is not JsonArray adimlar)
            {
                Hepsine(satirlar, grup, UygulamaDurumu.AdimYok,
                        $"{grup.Key} icinde Adimlar listesi yok.");
                continue;
            }

            var tiklaAdimlari = adimlar.OfType<JsonObject>()
                                       .Where(KoordinatKesfi.TiklaMi)
                                       .ToList();

            var degisti = false;

            foreach (var kayit in grup)
            {
                var sira = Sira(kayit.Anahtar);
                if (sira < 0 || sira >= tiklaAdimlari.Count)
                {
                    satirlar.Add(new UygulamaSatiri(kayit.Anahtar, UygulamaDurumu.AdimYok,
                        $"{grup.Key} icinde {sira + 1}. Tikla adimi yok."));
                    continue;
                }

                var adim = tiklaAdimlari[sira];
                var mevcutNot = KoordinatKesfi.Metin(adim, "Not");

                if (!NotEsliyor(kayit.Not, mevcutNot))
                {
                    satirlar.Add(new UygulamaSatiri(kayit.Anahtar, UygulamaDurumu.NotUyusmuyor,
                        $"{grup.Key}: adim aciklamasi degismis. Kayit \"{kayit.Not}\", " +
                        $"dosya \"{mevcutNot}\". Koordinat uygulanmadi, yeniden olcun."));
                    continue;
                }

                if (Ayni(KoordinatKesfi.Sayi(adim, "X"), kayit.X) &&
                    Ayni(KoordinatKesfi.Sayi(adim, "Y"), kayit.Y))
                {
                    satirlar.Add(new UygulamaSatiri(kayit.Anahtar, UygulamaDurumu.Ayni,
                        $"{grup.Key}: deger zaten guncel."));
                    continue;
                }

                AlanYaz(adim, "X", kayit.X);
                AlanYaz(adim, "Y", kayit.Y);
                degisti = true;

                satirlar.Add(new UygulamaSatiri(kayit.Anahtar, UygulamaDurumu.Uygulandi,
                    $"{grup.Key}: {OranDonusturucu.Yaz(kayit.X)} x {OranDonusturucu.Yaz(kayit.Y)} yazildi."));
            }

            // Dosyaya yalniz gercekten degistiyse dokunuluyor: her acilista butun
            // gorev dosyalarinin tarihini degistirmek, "neyi ne zaman elledim"
            // sorusunu cevapsiz birakirdi.
            if (degisti)
                File.WriteAllText(yol, kok.ToJsonString(Yazma));
        }

        return new UygulamaRaporu(satirlar);
    }

    private static void Hepsine(List<UygulamaSatiri> satirlar, IEnumerable<KoordinatAyari> kayitlar,
                                UygulamaDurumu durum, string mesaj)
    {
        foreach (var kayit in kayitlar)
            satirlar.Add(new UygulamaSatiri(kayit.Anahtar, durum, mesaj));
    }

    internal static string DosyaAdi(string anahtar)
    {
        var idx = anahtar.LastIndexOf('#');
        return idx <= 0 ? string.Empty : anahtar[..idx];
    }

    internal static int Sira(string anahtar)
    {
        var idx = anahtar.LastIndexOf('#');
        if (idx < 0 || idx + 1 >= anahtar.Length) return -1;
        return int.TryParse(anahtar[(idx + 1)..], out var sira) ? sira : -1;
    }

    /// <summary>
    /// Kayitta not yoksa kontrol yapilmiyor: aciklamasiz bir adimi kalibre eden
    /// kullaniciyi, sonradan aciklama eklenmesi yuzunden cezalandirmak anlamsiz.
    /// </summary>
    private static bool NotEsliyor(string kayitNotu, string dosyaNotu)
        => string.IsNullOrWhiteSpace(kayitNotu)
           || string.Equals(kayitNotu.Trim(), dosyaNotu.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Oran ucuncu haneye kadar yaziliyor; karsilastirma da o hassasiyette.</summary>
    private static bool Ayni(double a, double b) => Math.Abs(a - b) < 0.0005;

    private static void AlanYaz(JsonObject adim, string alan, double deger)
    {
        // Alan adi dosyada baska yazimla duruyor olabilir; motor buyuk/kucuk
        // harfe duyarsiz okuyor, biz de mevcut yazimi koruyoruz.
        foreach (var (ad, _) in adim.ToList())
        {
            if (!string.Equals(ad, alan, StringComparison.OrdinalIgnoreCase)) continue;
            adim[ad] = JsonValue.Create(Yuvarla(deger));
            return;
        }

        adim[alan] = JsonValue.Create(Yuvarla(deger));
    }

    /// <summary>
    /// Uc hane yeterli: 1920 piksel genislikte 0.001 oran bir pikselden az.
    /// Ham double yazmak dosyaya 0.30000000000000004 gibi degerler dusururdu.
    /// </summary>
    private static double Yuvarla(double oran) => Math.Round(oran, 3, MidpointRounding.AwayFromZero);
}
