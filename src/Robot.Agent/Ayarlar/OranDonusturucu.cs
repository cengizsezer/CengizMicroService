using System.Globalization;

namespace PkfRobot.Ayarlar;

/// <summary>
/// Bir pencerenin ekrandaki yeri ve olcusu. FlaUI'nin ya da WinForms'un kendi
/// dikdortgen tipi kullanilmadi: oran hesabi bu iki dunyanin ikisine de bagli
/// olmasin, test de ikisini kurmadan calissin.
/// </summary>
public readonly record struct PencereOlcusu(int Sol, int Ust, int Genislik, int Yukseklik)
{
    public bool Gecerli => Genislik > 0 && Yukseklik > 0;

    public int Sag => Sol + Genislik;
    public int Alt => Ust + Yukseklik;
}

/// <summary>
/// Mutlak ekran koordinati ile <b>pencereye goreli oran</b> arasindaki cevrim.
///
/// <b>Neden oran:</b> <c>Tikla</c> adimi (bkz. <c>AdimMotoru.Tikla</c>) X/Y'yi
/// pencerenin sol-ust kosesine ve olcusune oranla saklar. Ekran cozunurlugu ya
/// da pencere boyu degisince piksel kayar, oran kaymaz. Kalibrasyon seciciler
/// ayni hesabi <b>ters yonde</b> yapiyor; iki taraf ayrilirsa robotun tikladigi
/// yer ile kullanicinin sectigi yer sessizce ayrisir. Bu yuzden cevrim tek
/// yerde ve testli.
/// </summary>
public static class OranDonusturucu
{
    /// <summary>Mutlak ekran noktasi -> pencereye goreli oran.</summary>
    /// <exception cref="ArgumentException">Pencere olculeri okunamadiysa.</exception>
    public static (double X, double Y) Oran(int mutlakX, int mutlakY, PencereOlcusu pencere)
    {
        if (!pencere.Gecerli)
            throw new ArgumentException(
                $"Pencere olculeri gecersiz (G={pencere.Genislik}, Y={pencere.Yukseklik}). " +
                "Pencere simge durumunda kucultulmus olabilir.", nameof(pencere));

        var oranX = (mutlakX - pencere.Sol) / (double)pencere.Genislik;
        var oranY = (mutlakY - pencere.Ust) / (double)pencere.Yukseklik;

        return (oranX, oranY);
    }

    /// <summary>
    /// Oran -> mutlak ekran noktasi. <c>AdimMotoru.Tikla</c> ile <b>ayni</b>
    /// yuvarlama kullaniliyor; "Dene" dugmesinin tikladigi yer ile robotun
    /// tikladigi yer ayni piksel olsun.
    /// </summary>
    public static (int X, int Y) Mutlak(double oranX, double oranY, PencereOlcusu pencere)
    {
        if (!pencere.Gecerli)
            throw new ArgumentException(
                $"Pencere olculeri gecersiz (G={pencere.Genislik}, Y={pencere.Yukseklik}).",
                nameof(pencere));

        var x = (int)Math.Round(pencere.Sol + pencere.Genislik * oranX);
        var y = (int)Math.Round(pencere.Ust + pencere.Yukseklik * oranY);

        return (x, y);
    }

    /// <summary>Oran pencerenin icinde mi? 0..1 disi bir deger disariyi gosterir.</summary>
    public static bool OranIcerideMi(double oranX, double oranY)
        => oranX is >= 0 and <= 1 && oranY is >= 0 and <= 1;

    /// <summary>
    /// Orani JSON'a yapistirilabilir bicimde yazar. Ondalik ayrac her zaman
    /// NOKTA: Turkce locale virgul basar ve JSON bozulur (ayni gerekce
    /// <c>--kalibre</c> konsol modunda da yazili).
    /// </summary>
    public static string Yaz(double oran) => oran.ToString("0.###", CultureInfo.InvariantCulture);
}
