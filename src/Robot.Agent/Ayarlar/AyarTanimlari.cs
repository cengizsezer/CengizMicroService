namespace PkfRobot.Ayarlar;

public enum AyarTipi
{
    /// <summary>Gezginle secilen dosya. Var olmasi kontrol edilir.</summary>
    Dosya,

    /// <summary>Gezginle secilen klasor. Yoksa acilabilir.</summary>
    Klasor,

    /// <summary>Duz metin (firma kodu, kullanici kodu).</summary>
    Metin,

    /// <summary>Ekranda yildizli gosterilen, loglanmayan metin.</summary>
    Sifre
}

/// <summary>
/// Tek bir ayarin tanimi: ne oldugu, nasil girildigi, ne anlama geldigi.
/// </summary>
/// <param name="Anahtar">Kod icinde kullanilan sabit ad.</param>
/// <param name="Etiket">Ekranda gorunen ad.</param>
/// <param name="Tip">Girdi bicimi; arayuz dugmesini buna gore koyuyor.</param>
/// <param name="Aciklama">Alanin altinda gorunen tek cumlelik yardim.</param>
/// <param name="Varsayilan">Bos birakilirsa onerilen deger.</param>
/// <param name="Oku">Ayar nesnesinden degeri okuyan islev.</param>
/// <param name="Yaz">Ayar nesnesine degeri yazan islev.</param>
/// <param name="VarOlmali">Yol tipindeyse diskte bulunmasi bekleniyor mu?</param>
public record AyarTanimi(
    string Anahtar,
    string Etiket,
    AyarTipi Tip,
    string Aciklama,
    string Varsayilan,
    Func<RobotAyarlari, string> Oku,
    Action<RobotAyarlari, string> Yaz,
    bool VarOlmali = false)
{
    public bool YolMu => Tip is AyarTipi.Dosya or AyarTipi.Klasor;
}

/// <summary>
/// Ayarlarin tek listesi. <b>Arayuz bu listeden uretiliyor</b>: yeni bir ayar
/// eklemek icin buraya bir satir yazmak yetiyor, forma elle kutu koymak
/// gerekmiyor.
///
/// Listeyi tutmanin sebebi sadece kisalik degil: her alanin <b>aciklamasi</b>
/// tanimin yaninda duruyor. Alti ay sonra "bu kutuya ne yaziliyordu" sorusunun
/// cevabi ekranda, koda bakmadan gorunur olmali.
/// </summary>
public static class AyarTanimlari
{
    public const string OrkaExeYolu = "OrkaExeYolu";
    public const string IsDosyalariKlasoru = "IsDosyalariKlasoru";
    public const string LogKlasoru = "LogKlasoru";
    public const string FirmaKodu = "FirmaKodu";
    public const string KullaniciKodu = "KullaniciKodu";

    /// <summary>Yol ayarlarinin varsayilanlari; ilk acilista forma bunlar dusuyor.</summary>
    public static string VarsayilanIsKlasoru =>
        Path.Combine(PkfRobot.Ajan.AjanKimlikDeposu.VarsayilanKlasor, "isler");

    public static IReadOnlyList<AyarTanimi> Yollar { get; } = new[]
    {
        new AyarTanimi(
            OrkaExeYolu,
            "ORKA exe yolu",
            AyarTipi.Dosya,
            "ORKA'yi baslatan program. Ajan bu dosyayi calistiriyor; yanlissa is hic baslamaz.",
            @"C:\WinIceberg\OrkaWinIceberg.64.exe",
            a => a.OrkaExeYolu,
            (a, d) => a.OrkaExeYolu = d,
            VarOlmali: true),

        new AyarTanimi(
            IsDosyalariKlasoru,
            "Indirilen is dosyalari klasoru",
            AyarTipi.Klasor,
            "Sunucudan inen ekstre ve kod listesi buraya yaziliyor. Klasor yoksa acilir.",
            string.Empty,
            a => a.IsDosyalariKlasoru,
            (a, d) => a.IsDosyalariKlasoru = d),

        new AyarTanimi(
            LogKlasoru,
            "Log klasoru",
            AyarTipi.Klasor,
            "Gorev loglari ve ekran goruntuleri. Bir sey ters gittiginde once buraya bakilir.",
            @"C:\RobotLog",
            a => a.LogKlasoru,
            (a, d) => a.LogKlasoru = d)
    };

    public static IReadOnlyList<AyarTanimi> OrkaGirisi { get; } = new[]
    {
        new AyarTanimi(
            FirmaKodu,
            "Firma kodu",
            AyarTipi.Metin,
            "ORKA giris zincirinde F7'den sonra girilen kod (or. 0001).",
            string.Empty,
            a => a.FirmaKodu,
            (a, d) => a.FirmaKodu = d),

        new AyarTanimi(
            KullaniciKodu,
            "Kullanici kodu",
            AyarTipi.Metin,
            "ORKA kullanici kodu (or. pkf03).",
            string.Empty,
            a => a.KullaniciKodu,
            (a, d) => a.KullaniciKodu = d)
    };

    public static IReadOnlyList<AyarTanimi> Tumu { get; } = Yollar.Concat(OrkaGirisi).ToList();

    public static AyarTanimi? Bul(string anahtar)
        => Tumu.FirstOrDefault(t => string.Equals(t.Anahtar, anahtar, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Bos birakilmis yol ayarlarina varsayilanlarini koyar. Ilk acilista form
    /// bos gelmesin: kullanici "buraya ne yaziliyordu" diye dusunmesin, gordugu
    /// degeri duzeltsin.
    /// </summary>
    public static RobotAyarlari VarsayilanlariTamamla(RobotAyarlari ayarlar)
    {
        foreach (var tanim in Yollar)
        {
            if (!string.IsNullOrWhiteSpace(tanim.Oku(ayarlar))) continue;

            var varsayilan = tanim.Anahtar == IsDosyalariKlasoru ? VarsayilanIsKlasoru : tanim.Varsayilan;
            if (!string.IsNullOrWhiteSpace(varsayilan)) tanim.Yaz(ayarlar, varsayilan);
        }

        return ayarlar;
    }
}

/// <summary>
/// Yol ayarlarinin diskteki karsiligi var mi?
///
/// Kontrol kaydetmeyi <b>engellemiyor</b>: ORKA henuz kurulmamis bir makinede
/// ayarlari onceden girmek mesru. Kirmizi uyari cikiyor, karar kullanicinin.
/// </summary>
public static class YolDogrulama
{
    /// <summary>Sorun varsa aciklamasi, yoksa <c>null</c>.</summary>
    public static string? Sorun(AyarTanimi tanim, string? deger)
    {
        if (!tanim.YolMu) return null;

        if (string.IsNullOrWhiteSpace(deger))
            return tanim.VarOlmali ? $"{tanim.Etiket} bos." : null;

        try
        {
            return tanim.Tip switch
            {
                AyarTipi.Dosya when !File.Exists(deger) => $"Dosya bulunamadi: {deger}",
                AyarTipi.Klasor when !Directory.Exists(deger) =>
                    $"Klasor yok: {deger} (kaydedince acilir)",
                _ => null
            };
        }
        catch (Exception ex)
        {
            // Gecersiz karakter, erisilemeyen ag yolu: yolun kendisi sorunlu.
            return $"Yol okunamadi: {ex.Message}";
        }
    }

    /// <summary>Uyari mi, yoksa gercekten engelleyici bir eksik mi?</summary>
    public static bool Engelleyici(AyarTanimi tanim, string? deger)
        => tanim.VarOlmali && Sorun(tanim, deger) is not null;
}
