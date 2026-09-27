using System.Runtime.Versioning;
using FlaUI.UIA3;
using PkfRobot.Ayarlar;
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
    private readonly Action<AdimIlerlemesi>? _adimIlerledi;

    private readonly Action<Adim>? _onayBekleniyor;

    /// <param name="adimIlerledi">
    /// Sirasi gelen adim: kacinci/kac, ve NE oldugu. Yalniz (adim, toplam)
    /// ikilisi yetmiyordu -- "adim 12/38" satiri 45 saniye duran bir
    /// <c>AltPencereDogrula</c> ile 4 saniyelik bir <c>Bekle</c>'yi ayni
    /// gosteriyordu.
    /// </param>
    public GorevKosucusu(RobotConfig cfg, CalismaDenetimi denetim, double hizCarpani,
                         Action<string> log, Action<AdimIlerlemesi>? adimIlerledi = null,
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

        // Hangi degerlerle kosuldugu ajan yolundaki satirin AYNISI: iki yolu
        // karsilastirirken bicim farki, gercek bir fark saniliyordu.
        var zamanlamaOzeti = ZamanlamaCozumu.Ozet(_cfg, _hizCarpani, ZamanlamaCozumu.AyarlarKaynagi);

        _log($"{gorev.Ad}: {gorev.Adimlar.Count} adim. Log: {adimLog.Klasor}");
        _log($"Zamanlama: {zamanlamaOzeti}");
        adimLog.Bilgi($"Zamanlama: {zamanlamaOzeti}");

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
            _adimIlerledi?.Invoke(new AdimIlerlemesi(sayac, gorev.Adimlar.Count,
                                                     AdimMotoru.AdimEtiketi(adim)));
        });

        try
        {
            motor.Calistir(gorev);
        }
        finally
        {
            // Ozet formdaki log kutusuna da dusuyor: adim log'u yalnizca dosyaya
            // yaziliyor ve "hangi adim zaman yedi" sorusu ekranda cevaplanabilmeli.
            // Hata durumunda da yaziliyor -- yarim kosunun suresi de bilgi.
            var ozet = AdimSureOzeti.Ozet(motor.Olcumler);
            if (ozet.Length > 0)
                _log($"{gorev.Ad} bitti · toplam {SureBicimi.Kisa(motor.GecenSure)} · {ozet}");
        }
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
