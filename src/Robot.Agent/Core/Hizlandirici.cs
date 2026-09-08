using PkfRobot.Config;

namespace PkfRobot.Core;

/// <summary>
/// Gorevdeki <c>Bekle</c> adimlarini bir carpanla olcekler.
///
/// <b>Neden gorev dosyasina dokunmuyor:</b> JSON dosyalari akisin tek kaynagi ve
/// konsol modlari (<c>--gorev</c>, <c>--kalibre</c>, <c>--probe</c>) onlari
/// oldugu gibi calistirmaya devam etmeli. Carpan yalnizca Calistir sekmesinin
/// bellekteki KOPYASINA uygulaniyor; diskteki dosya degismiyor, bir sonraki
/// konsol calistirmasi carpandan etkilenmiyor.
///
/// <b>Neden Bekle adimlari VE tuslar arasi bekleme:</b> yavas gunde kirilan sey
/// sabit beklemeler; tuslar arasi bosluk (<c>Zamanlama.TusBeklemeMs</c>) da tam
/// olarak oyle bir bekleme. Onceden carpan ona hic dokunmuyordu ve "yavaslat"
/// dugmesi modul gezinmesinde hicbir sey degistirmiyordu -- carpanin
/// duzeltmedigi bir hata, carpan denenerek elenmis sayiliyor ve teshis
/// yaniltiliyordu.
///
/// Pencere TIMEOUT'lari yine disarida: onlar "ne kadar bekleyeyim" degil "ne
/// kadar sonra pes edeyim" sorusu ve formda ayri alanlar olarak duruyor.
/// </summary>
public static class Hizlandirici
{
    public const double EnAz = 0.1;
    public const double EnCok = 10.0;

    /// <summary>Carpani gecerli araliga cekar. Sifir/negatif deger beklemeyi yok ederdi.</summary>
    public static double Kirp(double carpan)
        => double.IsNaN(carpan) ? 1.0 : Math.Clamp(carpan, EnAz, EnCok);

    /// <summary>
    /// Carpan uygulanmis YENI bir gorev nesnesi. Carpan 1 ise gorev oldugu gibi
    /// doner (gereksiz kopya cikarmamak icin).
    /// </summary>
    public static Gorev Uygula(Gorev gorev, double carpan)
    {
        var oran = Kirp(carpan);
        if (Math.Abs(oran - 1.0) < 0.0001) return gorev;

        return new Gorev
        {
            Ad = gorev.Ad,
            Aciklama = gorev.Aciklama,
            Adimlar = gorev.Adimlar.Select(a => Olcekle(a, oran)).ToList()
        };
    }

    /// <summary>Tek adimin kopyasi; Bekle ise suresi olceklenmis halde.</summary>
    private static Adim Olcekle(Adim adim, double oran)
    {
        var kopya = new Adim
        {
            Tip = adim.Tip,
            Deger = adim.Deger,
            Adet = adim.Adet,
            Sayi = adim.Sayi,
            Not = adim.Not,
            AdetDegisken = adim.AdetDegisken,
            X = adim.X,
            Y = adim.Y,
            CiftTik = adim.CiftTik,
            Yuzde = adim.Yuzde,
            TimeoutSn = adim.TimeoutSn
        };

        if (BekleMi(adim) && adim.Sayi > 0)
            kopya.Sayi = Math.Max(0, (int)Math.Round(adim.Sayi * oran));

        return kopya;
    }

    public static bool BekleMi(Adim adim)
        => adim.Tip.Trim().Equals("Bekle", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Carpan uygulanmis config KOPYASI: yalnizca <c>Zamanlama.TusBeklemeMs</c>
    /// olcekleniyor.
    ///
    /// Kopya cikariliyor cunku ayni config nesnesi ayni anda calisan ajan isinde
    /// de kullanilabiliyor (bkz. <see cref="RobotConfig.CalismaKopyasi"/>);
    /// uzerine yazmak, carpani hic istemeyen bir isi de yavaslatirdi. Carpan 1
    /// ise nesne oldugu gibi doner.
    /// </summary>
    public static RobotConfig Uygula(RobotConfig cfg, double carpan)
    {
        var oran = Kirp(carpan);
        if (Math.Abs(oran - 1.0) < 0.0001) return cfg;

        var kopya = cfg.CalismaKopyasi();
        kopya.Zamanlama.TusBeklemeMs =
            Math.Max(0, (int)Math.Round(cfg.Zamanlama.TusBeklemeMs * oran));

        return kopya;
    }
}
