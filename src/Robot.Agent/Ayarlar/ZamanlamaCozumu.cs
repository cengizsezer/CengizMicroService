using System.Globalization;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.Ayarlar;

/// <summary>Cozulmus zamanlama: hangi degerlerle kosulacak ve bu degerler NEREDEN geldi.</summary>
/// <param name="Config">Calistirmada kullanilacak config KOPYASI (carpan uygulanmis).</param>
/// <param name="HizCarpani">Gorevdeki <c>Bekle</c> adimlarina uygulanacak carpan.</param>
/// <param name="Kaynak">Log'a yazilan kaynak adi.</param>
public sealed record ZamanlamaSonucu(RobotConfig Config, double HizCarpani, string Kaynak)
{
    /// <summary>Log'a tek satir olarak yazilan hali.</summary>
    public string Ozet => ZamanlamaCozumu.Ozet(Config, HizCarpani, Kaynak);
}

/// <summary>
/// "Bu calistirma hangi zamanlama degerleriyle kosacak" sorusunun TEK yeri.
///
/// <b>Neden gerekliydi:</b> Calistir sekmesindeki Adim/Tus/Pencere/ORKA acilis
/// degerleri ve hiz carpani yalnizca ELLE calistirmada (<see cref="Arayuz.GorevKosucusu"/>)
/// devredeydi. Ajan yolu (<see cref="Ajan.FlaUiOrkaSurucusu"/>) carpani hic
/// kullanmiyor, dogrudan appsettings.json'daki <c>Zamanlama</c> ile kosuyordu.
/// Sonuc: formdan "yavaslat" ya da "hizlandir" demek sunucudan gelen ise hicbir
/// sey yapmiyordu ve bu ekranda hicbir yerde gorunmuyordu.
///
/// <b>Oncelik: ayarlar.json &gt; appsettings.json.</b> ayarlar.json kullanicinin
/// ofiste olcup duzelttigi yer ve publish onun uzerine yazmiyor; appsettings.json
/// ise her yayinda gelen varsayilan. Dosya YOKSA (ilk kurulum, temiz makine)
/// appsettings degerleri gecerli kaliyor -- bos bir ayar nesnesinin varsayilanlari
/// sessizce appsettings'i ezmesin diye <see cref="AyarDeposu.VarMi"/> bakiliyor.
/// </summary>
public static class ZamanlamaCozumu
{
    public const string AyarlarKaynagi = "ayarlar.json (Calistir sekmesi)";
    public const string ConfigKaynagi = "appsettings.json";

    /// <summary>
    /// Diskteki Calistir sekmesi ayarlari; dosya yoksa <b>null</b> (= appsettings
    /// gecerli). Her calistirmada yeniden okunuyor: ajan gunlerce acik kaliyor ve
    /// arada formdan degistirilen bir deger bir sonraki iste gorulmeli -- firma
    /// sifrelerinin her iste yeniden okunmasiyla ayni gerekce.
    /// </summary>
    public static CalistirmaAyari? Diskten(string? klasor = null)
    {
        try
        {
            var depo = new AyarDeposu(klasor ?? AyarDeposu.VarsayilanKlasor);
            return depo.VarMi ? depo.Oku().Calistirma : null;
        }
        catch (Exception)
        {
            // Ayar dosyasi okunamiyorsa is DURMAMALI: appsettings degerleriyle
            // kosmak, "zamanlama okunamadi" diye aktarimi iptal etmekten iyi.
            return null;
        }
    }

    /// <summary>
    /// Zamanlamayi coz. <paramref name="ayar"/> null ise config oldugu gibi,
    /// carpan 1.0.
    /// </summary>
    public static ZamanlamaSonucu Coz(RobotConfig cfg, CalistirmaAyari? ayar)
    {
        if (ayar is null) return new ZamanlamaSonucu(cfg, 1.0, ConfigKaynagi);

        var kopya = cfg.CalismaKopyasi();

        kopya.Zamanlama.AdimBeklemeMs = Math.Max(0, ayar.AdimBeklemeMs);
        kopya.Zamanlama.TusBeklemeMs = Math.Max(0, ayar.TusBeklemeMs);
        kopya.Grid.GridTusBeklemeMs = Math.Max(0, ayar.GridTusBeklemeMs);

        // Timeout "ne kadar sonra pes edeyim" sorusu; sifir, robotun hicbir
        // pencereyi beklemeden hata vermesi olurdu.
        kopya.Zamanlama.PencereTimeoutSn = Math.Max(1, ayar.PencereTimeoutSn);
        kopya.Zamanlama.OrkaAcilisTimeoutSn = Math.Max(1, ayar.OrkaAcilisTimeoutSn);

        var carpan = Hizlandirici.Kirp(ayar.HizCarpani);

        return new ZamanlamaSonucu(Hizlandirici.Uygula(kopya, carpan), carpan, AyarlarKaynagi);
    }

    /// <summary>
    /// Log'un basina yazilan tek satir: hangi degerlerle kosuldugu.
    ///
    /// <b>Neden yaziliyor:</b> "formdan degistirdim ama degismedi" hatasi tam
    /// olarak burada gorulmedigi icin aylarca fark edilmedi. Satirdaki ms
    /// degerleri carpan UYGULANMIS hali, yani gercekten beklenecek sure.
    /// </summary>
    public static string Ozet(RobotConfig cfg, double carpan, string kaynak)
    {
        var satir = $"{kaynak} · hiz carpani " +
                    carpan.ToString("0.##", CultureInfo.InvariantCulture) +
                    $" · adim {cfg.Zamanlama.AdimBeklemeMs} ms" +
                    $" · tus {cfg.Zamanlama.TusBeklemeMs} ms" +
                    $" · grid tus {cfg.Grid.GridTusBeklemeMs} ms" +
                    $" · pencere {cfg.Zamanlama.PencereTimeoutSn} sn" +
                    $" · ORKA acilis {cfg.Zamanlama.OrkaAcilisTimeoutSn} sn";

        return Math.Abs(carpan - 1.0) < 0.0001
            ? satir
            : satir + " (tus ve grid degerleri carpan uygulanmis hali)";
    }
}
