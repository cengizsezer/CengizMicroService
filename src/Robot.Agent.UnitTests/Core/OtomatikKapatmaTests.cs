using PkfRobot.Arayuz;
using PkfRobot.Ayarlar;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// ORKA'nin yazici baglantisi diyalogu modul ekraninda rastgele cikiyor, odagi
/// aliyor ve o anda gonderilen tuslari yutup zinciri kiriyor. Cozum onu
/// BeklenmeyenPencereler'e koymak DEGIL: o liste robotu durdurur, oysa diyalogun
/// isle ilgisi yok -- kapatilip devam edilmeli.
///
/// Fare/klavye test edilemez ama <b>kural</b> edilebilir: ayarin okunmasi,
/// pencerenin listede yakalanmasi ve iki listenin karismamasi burada sabitleniyor.
/// </summary>
public class OtomatikKapatmaTests : IDisposable
{
    private readonly string _klasor;

    public OtomatikKapatmaTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-kapatma-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (IOException) { }
    }

    private RobotConfig AyarYukle(string icerik)
    {
        var yol = Path.Combine(_klasor, "appsettings.json");
        File.WriteAllText(yol, icerik);
        return RobotConfig.Yukle(yol);
    }

    private static UstSeviyePencere P(string baslik, bool gorunur = true)
        => new(new IntPtr(0x20), 4242, baslik, new PencereOlcusu(0, 0, 420, 180), gorunur, IntPtr.Zero);

    // ---- ayar okuma ----------------------------------------------------------

    [Fact]
    public void Ayar_dosyasindan_baslik_ve_dugme_okunuyor()
    {
        var cfg = AyarYukle("""
        {
          "OtomatikKapatilacakPencereler": [
            { "Baslik": "Yazici baglantisi", "Dugme": "Iptal" }
          ]
        }
        """);

        var kural = Assert.Single(cfg.OtomatikKapatilacakPencereler);
        Assert.Equal("Yazici baglantisi", kural.Baslik);
        Assert.Equal("Iptal", kural.Dugme);
    }

    [Fact]
    public void Dugme_verilmezse_bos_kaliyor()
    {
        // Bos dugme = "dogrudan ESC gonder". Kural yine gecerli.
        var cfg = AyarYukle("""
        {
          "OtomatikKapatilacakPencereler": [ { "Baslik": "Yazici baglantisi" } ]
        }
        """);

        Assert.Equal(string.Empty, cfg.OtomatikKapatilacakPencereler[0].Dugme);
    }

    [Fact]
    public void Bolum_hic_yoksa_liste_bos()
    {
        // Ofisteki eski appsettings.json bu bolumu tasimiyor; yeni surum onu
        // acamazsa robot hic baslamaz.
        var cfg = AyarYukle("""{ "OrkaPath": "C:\\WinIceberg\\OrkaWinIceberg.64.exe" }""");

        Assert.Empty(cfg.OtomatikKapatilacakPencereler);
    }

    [Fact]
    public void Yayindaki_ayarlarda_yazici_baglantisi_kurali_var()
    {
        var cfg = RobotConfig.Yukle(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        var kural = Assert.Single(
            cfg.OtomatikKapatilacakPencereler,
            k => k.Baslik.Contains("Yazici", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Iptal", kural.Dugme);
    }

    // ---- pencerenin yakalanmasi ---------------------------------------------

    [Fact]
    public void Yazici_diyalogu_pencere_listesinde_yakalaniyor()
    {
        // Gercek baslik kuraldaki parcadan uzun; eslestirme "icerir" mantiginda.
        var ekran = new List<UstSeviyePencere>
        {
            P("ORKA_0001_2026"),
            P("Yazici baglantisi kurulamadi"),
        };

        var bulunan = PencereBekleyici.Esles(ekran, "Yazici baglantisi");

        Assert.NotNull(bulunan);
        Assert.Equal("Yazici baglantisi kurulamadi", bulunan!.Baslik);
    }

    [Fact]
    public void Diyalog_ekranda_yokken_kapatilacak_pencere_bulunmuyor()
    {
        var ekran = new List<UstSeviyePencere> { P("ORKA_0001_2026") };

        Assert.Null(PencereBekleyici.Esles(ekran, "Yazici baglantisi"));
    }

    // ---- iki listenin karismamasi -------------------------------------------

    [Fact]
    public void Yayindaki_ayarlarda_ortusen_baslik_bildiriliyor()
    {
        // "Uyari Tanimlamalari" penceresi, durdurma listesindeki genel "Uyari"
        // kaydina da uyuyor. Bu bir AYAR HATASI DEGIL: o kaydi daraltmak ORKA'nin
        // gercek hata pencerelerini kacirmak olurdu. Ortusme bilerek var ve
        // gorev basinda log'a yaziliyor; davranisi asagidaki testler tanimliyor.
        var cfg = RobotConfig.Yukle(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.Equal(new[] { "Uyari Tanimlamalari" }, AdimMotoru.CakisanPencereBasliklari(cfg));
    }

    // ---- oncelik: once KAPAT, kapanmazsa DUR -------------------------------

    [Fact]
    public void Uyari_Tanimlamalari_penceresinin_kapatma_kurali_var()
    {
        // 08.09.2026: firma acilirken bu pencere cikiyor ve aktarim yarida
        // kaliyordu -- basligi "Uyari" kaydina uydugu icin robot DURUYORDU,
        // kapatma kurali hic denenmiyordu.
        var cfg = RobotConfig.Yukle(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        var kural = AdimMotoru.KapatmaKurali(cfg, "Uyari Tanimlamalari");

        Assert.NotNull(kural);
        Assert.Equal("Uyari Tanimlamalari", kural!.Baslik);
    }

    [Fact]
    public void Durdurma_kaydina_uyan_pencerenin_kapatma_kurali_bulunuyor()
    {
        // Ekrandaki gercek baslik kuraldan uzun olabilir; eslestirme iki yonlu.
        var cfg = new RobotConfig
        {
            BeklenmeyenPencereler = { "Uyari" },
            OtomatikKapatilacakPencereler =
            {
                new KapatilacakPencere { Baslik = "Uyari Tanimlamalari", Dugme = "Kapat" }
            }
        };

        Assert.NotNull(AdimMotoru.KapatmaKurali(cfg, "Uyari Tanimlamalari [ORKA]"));
    }

    [Fact]
    public void Kapatma_kurali_olmayan_pencere_dogrudan_durduruyor()
    {
        // Oncelik yalniz kurali OLAN pencereler icin: "Lisans" cikarsa robot
        // eskisi gibi duruyor.
        var cfg = RobotConfig.Yukle(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.Null(AdimMotoru.KapatmaKurali(cfg, "Lisans suresi doldu"));
        Assert.Null(AdimMotoru.KapatmaKurali(cfg, "Yedekleme"));
    }

    [Fact]
    public void Bos_baslikli_kural_hicbir_pencereye_uymuyor()
    {
        // Bos kural her basligi "icerir" -- her pencereyi kapatilabilir
        // gostermek, durdurma listesini tumden etkisiz kilardi.
        var cfg = new RobotConfig
        {
            OtomatikKapatilacakPencereler = { new KapatilacakPencere { Baslik = "  " } }
        };

        Assert.Null(AdimMotoru.KapatmaKurali(cfg, "Lisans"));
    }

    [Fact]
    public void Pencere_basligi_yoksa_kural_aranmiyor()
    {
        var cfg = RobotConfig.Yukle(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.Null(AdimMotoru.KapatmaKurali(cfg, null));
        Assert.Null(AdimMotoru.KapatmaKurali(cfg, "   "));
    }

    [Fact]
    public void Ayni_baslik_iki_listede_ise_cakisma_bildiriliyor()
    {
        var cfg = new RobotConfig
        {
            BeklenmeyenPencereler = { "Yazici baglantisi kurulamadi", "Lisans" },
            OtomatikKapatilacakPencereler =
            {
                new KapatilacakPencere { Baslik = "Yazici baglantisi", Dugme = "Iptal" }
            }
        };

        Assert.Equal(new[] { "Yazici baglantisi" }, AdimMotoru.CakisanPencereBasliklari(cfg));
    }

    [Fact]
    public void Farkli_basliklar_cakisma_sayilmiyor()
    {
        var cfg = new RobotConfig
        {
            BeklenmeyenPencereler = { "Lisans", "Yedekleme" },
            OtomatikKapatilacakPencereler =
            {
                new KapatilacakPencere { Baslik = "Yazici baglantisi", Dugme = "Iptal" }
            }
        };

        Assert.Empty(AdimMotoru.CakisanPencereBasliklari(cfg));
    }
}
