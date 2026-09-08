using System.Runtime.Versioning;
using FlaUI.UIA3;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.Arayuz;

/// <summary>
/// Tek bir gorev JSON'unu arayuz tarafindan calistirir.
///
/// <b>Adim motoru ve gorev dosyalari degismedi.</b> Bu sinif yalnizca motoru
/// kuruyor: degiskenleri veriyor, hiz carpanini bellekteki kopyaya uyguluyor ve
/// her adimin basinda DURDUR/DEVAM denetimini yokluyor. Ajan tarafindaki
/// <see cref="PkfRobot.Ajan.FlaUiOrkaSurucusu"/> ile ayni kalip; ayri olmasinin
/// sebebi arayuzun grid verisi olmadan ve kendi denetimiyle calismasi.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GorevKosucusu
{
    private readonly RobotConfig _cfg;
    private readonly CalismaDenetimi _denetim;
    private readonly double _hizCarpani;
    private readonly Action<string> _log;
    private readonly Action<int, int>? _adimIlerledi;

    private readonly Action<Adim>? _onayBekleniyor;

    public GorevKosucusu(RobotConfig cfg, CalismaDenetimi denetim, double hizCarpani,
                         Action<string> log, Action<int, int>? adimIlerledi = null,
                         Action<Adim>? onayBekleniyor = null)
    {
        // Carpan config'e BURADA, bir kez uygulaniyor: Calistir her banka icin
        // yeniden cagriliyor ve her cagrida olceklenseydi carpan katlanirdi.
        _cfg = Hizlandirici.Uygula(cfg, hizCarpani);
        _denetim = denetim;
        _hizCarpani = hizCarpani;
        _log = log;
        _adimIlerledi = adimIlerledi;
        _onayBekleniyor = onayBekleniyor;
    }

    /// <summary>Son calistirmanin log klasoru; ekran goruntuleri orada.</summary>
    public string? SonLogKlasoru { get; private set; }

    /// <param name="gridVerisi">
    /// <c>GridDoldur</c> adiminin yazacagi karsi hesap kodlari; null ise adim
    /// uyari yazip atlanir. Arayuzden calistirmada bu liste Calistir sekmesindeki
    /// "Karsi hesap kodlari" alanindan geliyor -- eskiden burasi her zaman null
    /// oldugu icin zincirin ORKA'ya veri yazan tek adimi elle HIC denenemiyordu.
    /// </param>
    public void Calistir(string gorevYolu, Dictionary<string, string> degiskenler,
                         GridDoldurVerisi? gridVerisi = null)
    {
        var gorev = Hizlandirici.Uygula(Gorev.Yukle(gorevYolu), _hizCarpani);

        using var automation = new UIA3Automation();
        using var adimLog = new AdimLogger(_cfg.LogKlasoru, gorev.Ad, _cfg.EkranGoruntusu.HerAdimda);

        SonLogKlasoru = adimLog.Klasor;

        // Tuslar arasi bosluk da log'a yaziliyor: carpanin gercekten etki ettigi
        // ofiste tek satirdan gorulebilsin diye.
        var carpanNotu = Math.Abs(_hizCarpani - 1.0) < 0.0001
            ? string.Empty
            : $" (hiz carpani {_hizCarpani:0.##} uygulandi; " +
              $"tuslar arasi {_cfg.Zamanlama.TusBeklemeMs} ms)";

        _log($"{gorev.Ad}: {gorev.Adimlar.Count} adim{carpanNotu}. Log: {adimLog.Klasor}");

        var sayac = 0;

        var motor = new AdimMotoru(_cfg, adimLog, automation, degiskenler, gridVerisi, adim =>
        {
            // "OnayBekle" adimi: once KENDILIGINDEN duraklatiliyor, sonra
            // asagidaki bekleme kullaniciyi bekliyor.
            OnayGerekiyorsaDuraklat(adim, _denetim, _log);
            if (adim.OnayBekle) _onayBekleniyor?.Invoke(adim);

            // Duraklatma/durdurma adimlar ARASINDA: yarim kalan bir adim ORKA'yi
            // bilinmeyen bir ekranda birakirdi.
            _denetim.AdimOncesiBekle();

            sayac++;
            _adimIlerledi?.Invoke(sayac, gorev.Adimlar.Count);
        });

        motor.Calistir(gorev);
    }

    /// <summary>
    /// <c>OnayBekle</c> tasiyan adimda akisi duraklatir; kullanici DEVAM'a
    /// basana kadar bir sonraki adim baslamaz.
    ///
    /// Zaten duraklatilmis ya da durdurulmus bir akisa dokunmaz: DURDUR sonrasi
    /// yeniden duraklatmak, kesme istegini yutardi.
    /// </summary>
    public static void OnayGerekiyorsaDuraklat(Adim adim, CalismaDenetimi denetim, Action<string> log)
    {
        if (!adim.OnayBekle) return;
        if (denetim.Hal != CalismaHali.Calisiyor) return;

        var ne = string.IsNullOrWhiteSpace(adim.Not) ? adim.Tip : adim.Not;
        log($"DURAKLATILDI - ORKA'da kontrol edip KAYDET'e basin, sonra DEVAM: {ne}");

        denetim.Duraklat();
    }
}
