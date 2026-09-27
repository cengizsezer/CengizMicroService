using PkfRobot.Ayarlar;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Calistir sekmesindeki zamanlama degerleri ve hiz carpani ELLE calistirmada
/// devredeydi, ajan yolunda DEGILDI: <c>FlaUiOrkaSurucusu</c> carpani hic
/// kullanmiyor, dogrudan appsettings.json'daki <c>Zamanlama</c> ile kosuyordu.
/// Formdan "yavaslat" demek sunucudan gelen ise hicbir sey yapmiyordu ve bu
/// hicbir yerde gorunmuyordu.
///
/// Burada sabitlenen kurallar: (1) ayarlar.json appsettings'i EZER, (2) ayar
/// dosyasi yoksa appsettings aynen gecerli kalir, (3) carpan tus ve grid
/// beklemelerine uygulanir ama timeout'lara uygulanmaz, (4) ozet satiri
/// GERCEKTEN kosulan degerleri yazar.
/// </summary>
public class ZamanlamaCozumuTests
{
    private static RobotConfig Appsettings() => new()
    {
        OrkaPath = @"C:\WinIceberg\OrkaWinIceberg.64.exe",
        Zamanlama = new ZamanlamaAyar
        {
            AdimBeklemeMs = 700,
            TusBeklemeMs = 150,
            PencereTimeoutSn = 40,
            OrkaAcilisTimeoutSn = 90
        },
        Grid = new GridAyar { SolaGitAdet = 12, TabAdet = 7, GridTusBeklemeMs = 80 }
    };

    private static CalistirmaAyari Form() => new()
    {
        AdimBeklemeMs = 300,
        TusBeklemeMs = 60,
        GridTusBeklemeMs = 40,
        PencereTimeoutSn = 25,
        OrkaAcilisTimeoutSn = 120,
        HizCarpani = 1.0
    };

    [Fact]
    public void Ayar_yoksa_appsettings_aynen_gecerli()
    {
        var cfg = Appsettings();
        var sonuc = ZamanlamaCozumu.Coz(cfg, null);

        Assert.Same(cfg, sonuc.Config);
        Assert.Equal(1.0, sonuc.HizCarpani, 5);
        Assert.Equal(ZamanlamaCozumu.ConfigKaynagi, sonuc.Kaynak);
    }

    [Fact]
    public void Ayarlar_json_appsettingsi_eziyor()
    {
        var sonuc = ZamanlamaCozumu.Coz(Appsettings(), Form());

        Assert.Equal(300, sonuc.Config.Zamanlama.AdimBeklemeMs);
        Assert.Equal(60, sonuc.Config.Zamanlama.TusBeklemeMs);
        Assert.Equal(40, sonuc.Config.Grid.GridTusBeklemeMs);
        Assert.Equal(25, sonuc.Config.Zamanlama.PencereTimeoutSn);
        Assert.Equal(120, sonuc.Config.Zamanlama.OrkaAcilisTimeoutSn);
        Assert.Equal(ZamanlamaCozumu.AyarlarKaynagi, sonuc.Kaynak);
    }

    [Fact]
    public void Orijinal_config_degismiyor()
    {
        // Ayni config nesnesi ayni anda calisan baska bir iste de kullanilabiliyor.
        var cfg = Appsettings();

        ZamanlamaCozumu.Coz(cfg, Form());

        Assert.Equal(150, cfg.Zamanlama.TusBeklemeMs);
        Assert.Equal(80, cfg.Grid.GridTusBeklemeMs);
    }

    [Fact]
    public void Hiz_carpani_tus_ve_grid_beklemesine_uygulaniyor()
    {
        var ayar = Form();
        ayar.HizCarpani = 2.0;

        var sonuc = ZamanlamaCozumu.Coz(Appsettings(), ayar);

        Assert.Equal(120, sonuc.Config.Zamanlama.TusBeklemeMs);   // 60 x 2
        Assert.Equal(80, sonuc.Config.Grid.GridTusBeklemeMs);     // 40 x 2
        Assert.Equal(2.0, sonuc.HizCarpani, 5);
    }

    [Fact]
    public void Carpan_timeoutlari_ve_adim_beklemesini_degistirmiyor()
    {
        var ayar = Form();
        ayar.HizCarpani = 3.0;

        var sonuc = ZamanlamaCozumu.Coz(Appsettings(), ayar);

        Assert.Equal(300, sonuc.Config.Zamanlama.AdimBeklemeMs);
        Assert.Equal(25, sonuc.Config.Zamanlama.PencereTimeoutSn);
        Assert.Equal(120, sonuc.Config.Zamanlama.OrkaAcilisTimeoutSn);
    }

    [Fact]
    public void Gecersiz_carpan_gecerli_araliga_cekiliyor()
    {
        var ayar = Form();
        ayar.HizCarpani = 0;

        var sonuc = ZamanlamaCozumu.Coz(Appsettings(), ayar);

        Assert.Equal(Hizlandirici.EnAz, sonuc.HizCarpani, 5);
    }

    [Fact]
    public void Timeout_sifir_verilse_bile_en_az_bir_saniye()
    {
        // Sifir timeout, robotun hicbir pencereyi beklemeden hata vermesi olurdu.
        var ayar = Form();
        ayar.PencereTimeoutSn = 0;
        ayar.OrkaAcilisTimeoutSn = 0;

        var sonuc = ZamanlamaCozumu.Coz(Appsettings(), ayar);

        Assert.Equal(1, sonuc.Config.Zamanlama.PencereTimeoutSn);
        Assert.Equal(1, sonuc.Config.Zamanlama.OrkaAcilisTimeoutSn);
    }

    [Fact]
    public void Grid_kolon_sayilari_appsettingsten_tasiniyor()
    {
        // CalismaKopyasi Grid'i hic kopyalamiyordu: kopya varsayilanlarla (12/7)
        // geliyor ve appsettings'te degistirilen kolon sayilari sessizce yok
        // sayiliyordu.
        var cfg = Appsettings();
        cfg.Grid.SolaGitAdet = 15;
        cfg.Grid.TabAdet = 9;

        var sonuc = ZamanlamaCozumu.Coz(cfg, Form());

        Assert.Equal(15, sonuc.Config.Grid.SolaGitAdet);
        Assert.Equal(9, sonuc.Config.Grid.TabAdet);
    }

    [Fact]
    public void Ozet_gercekten_kosulan_degerleri_yaziyor()
    {
        var ayar = Form();
        ayar.HizCarpani = 2.0;

        var ozet = ZamanlamaCozumu.Coz(Appsettings(), ayar).Ozet;

        Assert.Contains(ZamanlamaCozumu.AyarlarKaynagi, ozet);
        Assert.Contains("hiz carpani 2", ozet);
        Assert.Contains("tus 120 ms", ozet);      // carpan uygulanmis hali
        Assert.Contains("grid tus 80 ms", ozet);
        Assert.Contains("pencere 25 sn", ozet);
        Assert.Contains("ORKA acilis 120 sn", ozet);
    }

    [Fact]
    public void Ozet_carpan_yokken_carpan_notu_yazmiyor()
    {
        var ozet = ZamanlamaCozumu.Coz(Appsettings(), Form()).Ozet;

        Assert.DoesNotContain("carpan uygulanmis hali", ozet);
        Assert.Contains("tus 60 ms", ozet);
    }
}
