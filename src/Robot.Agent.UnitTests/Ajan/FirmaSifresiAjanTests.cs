using ClosedXML.Excel;
using PkfRobot.Ajan;
using PkfRobot.Ayarlar;
using PkfRobot.Config;
using PkfRobot.Core;
using System.Text.Json;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>
/// Ajan yolu ARTIK firma bazli sifre deposunu okuyor.
///
/// Once buraya <c>appsettings.Giris</c>'teki TEK alan geliyordu: sunucudan hangi
/// firma gelirse gelsin ayni sifre deneniyordu. Birden fazla firmayla calisirken
/// bu, ikinci firmaya birincinin sifresiyle girmeye calismak demek -- ve yanlis
/// sifre ORKA'yi kilitliyor.
///
/// <b>Sunucudan sifre GELMIYOR.</b> Is paketinde yalnizca firma KODU var; sifreler
/// bu makinede DPAPI ile sifreli duruyor. Cozum yalnizca YEREL depodan yapiliyor.
/// </summary>
public class FirmaSifresiAjanTests : IDisposable
{
    private readonly string _klasor;

    public FirmaSifresiAjanTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-sifre-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch { }
    }

    // ---- asil davranis ------------------------------------------------------

    [Fact]
    public async Task Firma_bazli_sifre_goreve_gecirilliyor()
    {
        // Depoda iki firma var; is 0336 icin geldi, 0336'nin sifreleri gitmeli.
        var depo = new Sifreler();
        depo.OrkaSifresiYaz("0001", "aday-orka");
        depo.FirmaSifresiYaz("0001", "aday-firma");
        depo.OrkaSifresiYaz("0336", "smmm-orka");
        depo.FirmaSifresiYaz("0336", "smmm-firma");

        var (sonuc, surucu, _, _) = await Calistir(depo, firmaKodu: "0336");

        Assert.True(sonuc.Basarili);
        Assert.Equal("smmm-orka", surucu.SonIstek!.Degiskenler["sifre"]);
        Assert.Equal("smmm-firma", surucu.SonIstek.Degiskenler["firmaSifre"]);
    }

    [Fact]
    public async Task Firma_kaydi_yoksa_appsettings_yedegi_kullaniliyor_ve_log_bunu_yaziyor()
    {
        // #4: geriye donuk uyumluluk. Tek firmayla calisan eski kurulum bozulmasin.
        var (sonuc, surucu, log, _) = await Calistir(new Sifreler(), "0001",
                                                     Config("app-orka", "app-firma"));

        Assert.True(sonuc.Basarili);
        Assert.Equal("app-orka", surucu.SonIstek!.Degiskenler["sifre"]);
        Assert.Contains(log.Satirlar, s => s.Contains("appsettings.json") && s.Contains("YEDEK"));
    }

    // ---- eksik kayit: is HIC BASLAMIYOR -------------------------------------

    [Fact]
    public async Task Sifre_kaydi_yoksa_is_baslamadan_hata_veriyor()
    {
        var (sonuc, surucu, log, dosyalar) = await Calistir(new Sifreler(), "0500", Config("", ""));

        Assert.False(sonuc.Basarili);
        Assert.Contains("0500 firmasi icin sifre kaydi yok", sonuc.HataMesaji);
        Assert.Contains("Calistir sekmesinden girin", sonuc.HataMesaji);
        Assert.Contains(log.Satirlar, s => s.Contains("0500 firmasi icin sifre kaydi yok"));

        // ORKA'ya HIC dokunulmadi ve dosyalar bile indirilmedi: sifresiz giris
        // ekrani gecilemez, yarim kalan bir oturum birakmanin anlami yok.
        Assert.Equal(0, surucu.CalistirmaSayisi);
        Assert.Equal(0, dosyalar.EkstreIndirmeSayisi);
    }

    [Fact]
    public async Task Yalnizca_firma_sifresi_eksikse_de_is_baslamiyor()
    {
        // Giris ekrani gecilir ama firma acilamaz; yarim oturum birakilmasin.
        var depo = new Sifreler();
        depo.OrkaSifresiYaz("0001", "orka");

        var (sonuc, surucu, _, _) = await Calistir(depo, "0001", Config("", ""));

        Assert.False(sonuc.Basarili);
        Assert.Contains("firma sifresi", sonuc.HataMesaji);
        Assert.Equal(0, surucu.CalistirmaSayisi);
    }

    [Fact]
    public async Task Depo_okunamazsa_uyari_yazilip_yedege_dusuluyor()
    {
        // Baska Windows kullanicisi ya da bozuk dosya: is durmasin, sebep gorunsun.
        var (sonuc, _, log, _) = await Calistir(null, "0001", Config("app-orka", "app-firma"),
                                                depoPatlasin: true);

        Assert.True(sonuc.Basarili);
        Assert.Contains(log.Satirlar, s => s.Contains("sifre deposu okunamadi"));
    }

    [Fact]
    public async Task Sifreler_log_a_DUZ_yazilmiyor()
    {
        var depo = new Sifreler();
        depo.OrkaSifresiYaz("0001", "cok-gizli-orka");
        depo.FirmaSifresiYaz("0001", "cok-gizli-firma");

        var (_, _, log, _) = await Calistir(depo, "0001");

        Assert.DoesNotContain("cok-gizli", log.Tumu);
    }

    // ---- kurulum ------------------------------------------------------------

    private static RobotConfig Config(string sifre, string firmaSifresi)
    {
        var cfg = new RobotConfig();
        cfg.Giris.Sifre = sifre;
        cfg.Giris.FirmaSifresi = firmaSifresi;
        return cfg;
    }

    private async Task<(IsSonucu Sonuc, IzliSurucu Surucu, ListeLog Log, SayanDosyalar Dosyalar)> Calistir(
        Sifreler? depo, string firmaKodu, RobotConfig? cfg = null, bool depoPatlasin = false)
    {
        var log = new ListeLog();
        var dosyalar = new SayanDosyalar { EkstreYolu = Ekstre(3), Liste = Liste(3) };
        var surucu = new IzliSurucu();

        Func<Sifreler> oku = depoPatlasin
            ? () => throw new InvalidOperationException("dosya bozuk")
            : () => depo ?? new Sifreler();

        var calistirici = new OrkayaAktarCalistirici(
            cfg ?? Config("", ""), dosyalar, surucu, new SahteOrka(), log,
            Path.Combine(_klasor, "isler"),
            sifreleriOku: oku);

        var sonuc = await calistirici.CalistirAsync(Paket(firmaKodu), new SessizIlerleme(),
                                                    CancellationToken.None);
        return (sonuc, surucu, log, dosyalar);
    }

    private static AjanIsPaketi Paket(string firmaKodu) => new()
    {
        IsId = Guid.NewGuid(),
        IsTipi = OrkayaAktarCalistirici.Tip,
        FirmaId = 201,
        Yuk = JsonSerializer.Serialize(new OrkayaAktarYuku
        {
            EkstreYuklemeId = 12,
            FirmaId = 201,
            BankaHesabiOrkaKodu = "102 1 1 01",
            FirmaKodu = firmaKodu,
            SatirSayisi = 3
        })
    };

    private static KodListesi Liste(int satirSayisi) => new()
    {
        EkstreId = 12,
        SatirSayisi = satirSayisi,
        Satirlar = Enumerable.Range(1, satirSayisi)
            .Select(i => new OrkaSatiri
            {
                SiraNo = i,
                Aciklama = "ORNEK ODEME",
                KarsiHesapKodu = "320 A01",
                BankaHesapKodu = "102 1 1 01"
            }).ToList()
    };

    private string Ekstre(int veriSatiri)
    {
        var yol = Path.Combine(_klasor, $"ekstre-{Guid.NewGuid():N}.xlsx");

        using var kitap = new XLWorkbook();
        var sayfa = kitap.Worksheets.Add("Ekstre");
        sayfa.Cell(1, 1).Value = "Tarih";
        sayfa.Cell(1, 2).Value = "Aciklama";

        for (var i = 0; i < veriSatiri; i++)
        {
            sayfa.Cell(i + 2, 1).Value = new DateTime(2026, 1, i % 28 + 1);
            sayfa.Cell(i + 2, 2).Value = "ORNEK ODEME";
        }

        kitap.SaveAs(yol);
        return yol;
    }

    // ---- sahteler -----------------------------------------------------------

    /// <summary>Indirme SAYISINI da tutuyor: "hic baslamadi" bunun uzerinden olculuyor.</summary>
    private sealed class SayanDosyalar : IIsDosyalari
    {
        public string EkstreYolu { get; set; } = "";
        public KodListesi Liste { get; set; } = new();
        public int EkstreIndirmeSayisi { get; private set; }

        public Task<string> EkstreIndirAsync(Guid isId, string klasor, CancellationToken ct)
        {
            EkstreIndirmeSayisi++;
            return Task.FromResult(EkstreYolu);
        }

        public Task<KodListesi> KodListesiIndirAsync(Guid isId, CancellationToken ct)
            => Task.FromResult(Liste);

        public Task<string?> HataEkraniYukleAsync(string dosyaYolu, CancellationToken ct)
            => Task.FromResult<string?>(null);
    }

    /// <summary>Goreve gecen degiskenleri saklayan surucu.</summary>
    private sealed class IzliSurucu : IOrkaSurucusu
    {
        public OrkaAktarimIstegi? SonIstek { get; private set; }
        public int CalistirmaSayisi { get; private set; }
        public string? SonEkranGoruntusuYolu => null;

        public Task CalistirAsync(OrkaAktarimIstegi istek, GridDoldurVerisi grid,
                                  Action<Adim> adimBasladi, CancellationToken ct)
        {
            CalistirmaSayisi++;
            SonIstek = istek;
            grid.YazilanSatir = grid.Satirlar.Count;
            return Task.CompletedTask;
        }
    }

    private sealed class SessizIlerleme : IIsIlerleme
    {
        public Task BildirAsync(int yuzde, string mesaj, int? tamamlananAdim, CancellationToken ct)
            => Task.CompletedTask;
    }
}
