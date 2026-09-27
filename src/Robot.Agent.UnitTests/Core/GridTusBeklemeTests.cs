using FlaUI.Core.WindowsAPI;
using PkfRobot.Ayarlar;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Grid'in tuslar arasi beklemesi <c>Zamanlama.TusBeklemeMs</c> ile ORTAKTI:
/// 150 ms (hiz carpaniyla 225 ms) modul gezinmesi icin secilmis bir deger ama
/// grid'de satir basina 5 tus dusuyor -- 43 satirlik bir ekstrede 215 bekleme,
/// yalnizca bekleme olarak 48 saniye. Ortak deger, modul gezinmesini de
/// kisaltmadan grid'i hizlandirmayi imkansiz kiliyordu.
///
/// Burada sabitlenen: GridDoldur KENDI degerini kullanir, diger adimlar
/// <c>TusBeklemeMs</c>'i kullanmaya devam eder.
/// </summary>
[Collection(KlavyeKoleksiyonu.Ad)]
public class GridTusBeklemeTests : IDisposable
{
    private readonly SahteKlavye _sahte = new();
    private readonly int _eskiBekleme = Klavye.VarsayilanBeklemeMs;

    public GridTusBeklemeTests()
    {
        Klavye.Surucu = _sahte;
        // Diger adimlarin degeri; grid bunu KULLANMAMALI.
        Klavye.VarsayilanBeklemeMs = 150;
    }

    public void Dispose()
    {
        Klavye.SurucuyuSifirla();
        Klavye.VarsayilanBeklemeMs = _eskiBekleme;
    }

    private sealed class SahteKlavye : IKlavyeSurucusu
    {
        public readonly List<int> Beklemeler = new();

        public void TusaBas(VirtualKeyShort tus) { }
        public void MetinYaz(string metin) { }
        public void BirlikteBas(IReadOnlyList<VirtualKeyShort> tuslar) { }
        public void Bekle(int ms) => Beklemeler.Add(ms);
    }

    private static IReadOnlyList<GridSatiri> Satirlar(params string[] kodlar)
        => kodlar.Select((k, i) => new GridSatiri(i + 1, $"aciklama {i + 1}", k)).ToList();

    [Fact]
    public void Grid_kendi_beklemesini_kullaniyor()
    {
        AdimMotoru.GridiYaz(Satirlar("100 01", "600 01"), solaGitAdet: 2, tabAdet: 1,
                            tusBeklemeMs: 40);

        Assert.All(_sahte.Beklemeler, ms => Assert.Equal(40, ms));
        Assert.DoesNotContain(150, _sahte.Beklemeler);
    }

    [Fact]
    public void Deger_verilmezse_ortak_bekleme_suruyor()
    {
        // Konsol modlari ve mevcut cagiranlar icin davranis degismedi.
        AdimMotoru.GridiYaz(Satirlar("100 01"), solaGitAdet: 1, tabAdet: 1);

        Assert.All(_sahte.Beklemeler, ms => Assert.Equal(150, ms));
    }

    [Fact]
    public void Satir_basina_bes_bekleme_dusuyor()
    {
        // Yaz (1) + ENTER (once/sonra 2) + DOWN (once/sonra 2) = 5.
        // Ayarin neden ayri oldugunu anlatan sayi bu; degisirse gorulmeli.
        AdimMotoru.GridiYaz(Satirlar("A", "B", "C"), solaGitAdet: 0, tabAdet: 0,
                            tusBeklemeMs: 10);

        Assert.Equal(15, _sahte.Beklemeler.Count);
    }

    [Fact]
    public void Sifir_bekleme_de_verilebiliyor()
    {
        AdimMotoru.GridiYaz(Satirlar("A"), 0, 0, tusBeklemeMs: 0);

        Assert.All(_sahte.Beklemeler, ms => Assert.Equal(0, ms));
    }

    // ---- ayar tarafi --------------------------------------------------------

    [Fact]
    public void Varsayilan_80_ms()
    {
        Assert.Equal(80, new GridAyar().GridTusBeklemeMs);
        Assert.Equal(80, new CalistirmaAyari().GridTusBeklemeMs);
    }

    [Fact]
    public void Hiz_carpani_grid_beklemesine_de_uygulaniyor()
    {
        // "Yavas gun" dugmesi gorevin en cok tus gonderen adimini disarida
        // birakmamali.
        var cfg = new RobotConfig
        {
            Zamanlama = new ZamanlamaAyar { TusBeklemeMs = 150 },
            Grid = new GridAyar { GridTusBeklemeMs = 80 }
        };

        var sonuc = Hizlandirici.Uygula(cfg, 1.5);

        Assert.Equal(120, sonuc.Grid.GridTusBeklemeMs);
        Assert.Equal(225, sonuc.Zamanlama.TusBeklemeMs);
        Assert.Equal(80, cfg.Grid.GridTusBeklemeMs);   // orijinal degismedi
    }

    [Fact]
    public void Calisma_kopyasi_grid_ayarini_tasiyor()
    {
        // Grid, CalismaKopyasi'nda hic kopyalanmiyordu: kopya varsayilanlarla
        // geliyor ve appsettings'teki degerler Calistir sekmesinde yok sayiliyordu.
        var cfg = new RobotConfig
        {
            Grid = new GridAyar { SolaGitAdet = 15, TabAdet = 9, GridTusBeklemeMs = 25 }
        };

        var kopya = cfg.CalismaKopyasi();
        kopya.Grid.GridTusBeklemeMs = 5;

        Assert.Equal(15, kopya.Grid.SolaGitAdet);
        Assert.Equal(9, kopya.Grid.TabAdet);
        Assert.Equal(25, cfg.Grid.GridTusBeklemeMs);   // paylasilan nesne degismedi
    }
}
