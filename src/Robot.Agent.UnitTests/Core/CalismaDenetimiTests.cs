using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// DURDUR / DEVAM'in akistaki karsiligi. Adim motoru degismedi; denetim her
/// adimin basindaki geri cagirmaya takiliyor.
///
/// Testler gercek ipliklerle calisiyor cunku sinanan sey tam olarak bu:
/// duraklatilmis bir akis GERCEKTEN bekliyor mu, DEVAM onu birakiyor mu.
/// </summary>
public class CalismaDenetimiTests
{
    private static readonly TimeSpan Sabir = TimeSpan.FromSeconds(5);

    [Fact]
    public void Varsayilan_halde_akis_beklemiyor()
    {
        var denetim = new CalismaDenetimi();

        denetim.AdimOncesiBekle();   // patlamamali

        Assert.Equal(CalismaHali.Calisiyor, denetim.Hal);
    }

    [Fact]
    public void Durdurulunca_adim_basinda_istisna_atiyor()
    {
        var denetim = new CalismaDenetimi();
        denetim.Durdur();

        Assert.Throws<CalismaDurduruldu>(() => denetim.AdimOncesiBekle());
        Assert.Equal(CalismaHali.Durduruldu, denetim.Hal);
    }

    [Fact]
    public void Duraklatilan_akis_devam_gelene_kadar_bekliyor()
    {
        var denetim = new CalismaDenetimi();
        var girdi = new ManualResetEventSlim(false);
        var cikti = new ManualResetEventSlim(false);

        denetim.Duraklat();

        var is_ = Task.Run(() =>
        {
            girdi.Set();
            denetim.AdimOncesiBekle();
            cikti.Set();
        });

        Assert.True(girdi.Wait(Sabir));

        // Duraklatilmisken gecmemeli.
        Assert.False(cikti.Wait(TimeSpan.FromMilliseconds(300)));
        Assert.Equal(CalismaHali.Duraklatildi, denetim.Hal);

        denetim.Devam();

        Assert.True(cikti.Wait(Sabir));
        Assert.True(is_.Wait(Sabir));
        Assert.Equal(CalismaHali.Calisiyor, denetim.Hal);
    }

    [Fact]
    public void Duraklatilmisken_durdurmak_bekleyen_adimi_uyandiriyor()
    {
        // DURDUR duraklatilmis akisi da kesmeli; yoksa geride sonsuza kadar
        // bekleyen bir gorev parcacigi kalirdi.
        var denetim = new CalismaDenetimi();
        denetim.Duraklat();

        var is_ = Task.Run(() => denetim.AdimOncesiBekle());

        Thread.Sleep(200);
        denetim.Durdur();

        var hata = Assert.Throws<AggregateException>(() => is_.Wait(Sabir));
        Assert.IsType<CalismaDurduruldu>(hata.InnerException);
    }

    [Fact]
    public void Devam_kaldigi_adimdan_suruyor()
    {
        // Ilk iki adim gecti, uculerin basinda duraklatildi: DEVAM'dan sonra
        // ucuncu adimdan devam etmeli, bastan baslamamali.
        var denetim = new CalismaDenetimi();
        var calisan = new List<int>();
        var ucuncudeDuraklat = new ManualResetEventSlim(false);

        var is_ = Task.Run(() =>
        {
            for (var adim = 1; adim <= 5; adim++)
            {
                if (adim == 3) ucuncudeDuraklat.Set();
                denetim.AdimOncesiBekle();
                lock (calisan) calisan.Add(adim);
                Thread.Sleep(20);
            }
        });

        Assert.True(ucuncudeDuraklat.Wait(Sabir));
        denetim.Duraklat();

        Thread.Sleep(300);
        int gecen;
        lock (calisan) gecen = calisan.Count;

        Assert.True(gecen < 5, $"Duraklatilmasina ragmen {gecen} adim gecti.");

        denetim.Devam();
        Assert.True(is_.Wait(Sabir));

        lock (calisan)
        {
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, calisan);   // hicbir adim tekrarlanmadi
        }
    }

    [Fact]
    public void Sifirla_onceki_turun_durdurmasini_temizliyor()
    {
        var denetim = new CalismaDenetimi();
        denetim.Durdur();

        denetim.Sifirla();

        denetim.AdimOncesiBekle();   // yeni kuyruk baslayabilmeli
        Assert.Equal(CalismaHali.Calisiyor, denetim.Hal);
    }

    [Fact]
    public void Hal_degisimi_ekrana_bildiriliyor()
    {
        var denetim = new CalismaDenetimi();
        var bildirim = 0;
        denetim.Degisti += () => Interlocked.Increment(ref bildirim);

        denetim.Duraklat();
        denetim.Devam();
        denetim.Durdur();

        Assert.Equal(3, bildirim);
    }
}
