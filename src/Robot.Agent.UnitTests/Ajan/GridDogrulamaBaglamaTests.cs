using ClosedXML.Excel;
using PkfRobot.Ajan;
using PkfRobot.Config;
using PkfRobot.Core;
using System.Text.Json;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>
/// Dogrulama katmaninin ise BAGLANMA kurallari.
///
/// Tek bir cumleyle sinanan sey: <b>bu katman isi hicbir kosulda basarisiz
/// yapmiyor.</b> Kapaliyken hic cagrilmiyor; acikken uyusmazlik bulsa da,
/// okuyucu bos donse de, goruntu alinamamis olsa da is BASARILI bitiyor --
/// Kaydet'e zaten kullanici basiyor, bu bir on eleme.
/// </summary>
public class GridDogrulamaBaglamaTests : IDisposable
{
    private const string Kod = "320 A01";

    private readonly string _klasor;

    public GridDogrulamaBaglamaTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-dogrulama-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch { }
    }

    // ---- kurulum ------------------------------------------------------------

    private static KodListesi Liste(int satir) => new()
    {
        EkstreId = 12,
        SatirSayisi = satir,
        Satirlar = Enumerable.Range(1, satir).Select(i => new OrkaSatiri
        {
            SiraNo = i,
            Aciklama = "ORNEK ODEME",
            KarsiHesapKodu = Kod,
            BankaHesapKodu = "102 1 1 01"
        }).ToList()
    };

    /// <summary>Sunucunun urettigiyle ayni yapida bir xlsx: baslik + veri satirlari.</summary>
    private string Ekstre(int veriSatiri)
    {
        var yol = Path.Combine(_klasor, $"ekstre-{Guid.NewGuid():N}.xlsx");

        using var kitap = new XLWorkbook();
        var sayfa = kitap.Worksheets.Add("Ekstre");
        sayfa.Cell(1, 1).Value = "Tarih";
        sayfa.Cell(1, 2).Value = "Açıklama";

        for (var i = 0; i < veriSatiri; i++)
        {
            sayfa.Cell(i + 2, 1).Value = new DateTime(2026, 1, i % 28 + 1);
            sayfa.Cell(i + 2, 2).Value = "ORNEK ODEME";
        }

        kitap.SaveAs(yol);
        return yol;
    }

    private static AjanIsPaketi Paket(int satir) => new()
    {
        IsId = Guid.NewGuid(),
        IsTipi = OrkayaAktarCalistirici.Tip,
        FirmaId = 201,
        Yuk = JsonSerializer.Serialize(new OrkayaAktarYuku
        {
            EkstreYuklemeId = 12,
            FirmaId = 201,
            BankaHesabiOrkaKodu = "102 1 1 01",
            FirmaKodu = "0001",
            SatirSayisi = satir
        })
    };

    /// <param name="goruntuVar">
    /// false ise surucu goruntu yolu birakmiyor: ekran goruntusu alinamamis hali.
    /// </param>
    private async Task<(IsSonucu Sonuc, ListeLog Log)> CalistirAsync(
        IGridOkuyucu? okuyucu, int satir = 3, bool goruntuVar = true)
    {
        var log = new ListeLog();
        var dosyalar = new SahteDosyalar { EkstreYolu = Ekstre(satir), Liste = Liste(satir) };

        string? goruntu = null;
        if (goruntuVar)
        {
            // Goruntu yolu normalde AdimMotoru'ndan geliyor; surucunun sahtesi taklit ediyor.
            goruntu = Path.Combine(_klasor, $"griddoldur-sonrasi-{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(goruntu, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        }

        var calistirici = new OrkayaAktarCalistirici(
            SifreliConfig(), dosyalar, new SahteSurucu { GoruntuYolu = goruntu },
            new SahteOrka(), log, Path.Combine(_klasor, "isler"),
            gridOkuyucu: okuyucu);

        var sonuc = await calistirici.CalistirAsync(Paket(satir), new SessizIlerleme(), CancellationToken.None);
        return (sonuc, log);
    }

    /// <summary>
    /// Sifresi olan config. Aktarim, sifre cozulemezse ORKA'ya HIC dokunmadan
    /// duruyor (bkz. <see cref="PkfRobot.UnitTests.Ajan.FirmaSifresiAjanTests"/>);
    /// buradaki testler o kapinin OTESINI olctugu icin yedek sifre veriliyor.
    /// </summary>
    private static RobotConfig SifreliConfig()
    {
        var cfg = new RobotConfig();
        cfg.Giris.Sifre = "orka";
        cfg.Giris.FirmaSifresi = "firma";
        return cfg;
    }

    private static SahteOkuyucu Okur(params string[] kodlar) => new()
    {
        Okuma = new GridOkumasi(
            kodlar.Select((k, i) => new OkunanGridSatiri(i + 1, "ORNEK ODEME", k)).ToList(),
            Kesildi: false)
    };

    // ---- kapali hali --------------------------------------------------------

    [Fact]
    public async Task Okuyucu_yoksa_hic_dogrulama_yapilmiyor()
    {
        // Ozellik kapaliyken (ya da anahtar yokken) buraya null geliyor: log'da
        // dogrulama izi olmamali, ozet alani bos kalmali, akis eskisi gibi olmali.
        var (sonuc, log) = await CalistirAsync(okuyucu: null);

        Assert.True(sonuc.Basarili);
        Assert.DoesNotContain("Dogrulama:", log.Tumu);
        Assert.Contains("\"Dogrulama\":null", sonuc.SonucOzetiJson);
    }

    // ---- acik hali ----------------------------------------------------------

    [Fact]
    public async Task Hepsi_tutuyorsa_tek_satir_log_ve_ozete_yaziliyor()
    {
        var okuyucu = Okur(Kod, Kod, Kod);
        var (sonuc, log) = await CalistirAsync(okuyucu);

        Assert.True(sonuc.Basarili);
        Assert.Equal(1, okuyucu.CagriSayisi);
        Assert.Contains("Dogrulama: 3/3 satir eslesti.", log.Tumu);
        Assert.Contains("3/3 satir eslesti", sonuc.SonucOzetiJson);
    }

    [Fact]
    public async Task Uyusmazlikta_UYARI_yaziliyor_ama_is_BASARILI_bitiyor()
    {
        // Katmanin en onemli kurali: robot DURMUYOR.
        var (sonuc, log) = await CalistirAsync(Okur(Kod, "999 99", Kod));

        Assert.True(sonuc.Basarili);
        Assert.Null(sonuc.HataMesaji);

        Assert.Contains("DOGRULAMA UYARISI", log.Tumu);
        Assert.Contains("beklenen '320 A01', okunan '999 99'", log.Tumu);
        Assert.Contains("TUTMUYOR", sonuc.SonucOzetiJson);
    }

    [Fact]
    public async Task Okuyucu_bos_donerse_is_BASARILI_ve_ozette_yapilamadi()
    {
        // Ag hatasi / kota / zaman asimi: okuyucu bos doner, sebebini kendi loglar.
        var (sonuc, log) = await CalistirAsync(new SahteOkuyucu { Okuma = GridOkumasi.Bos });

        Assert.True(sonuc.Basarili);
        Assert.Contains("\"Dogrulama\":\"yapilamadi\"", sonuc.SonucOzetiJson);
        Assert.DoesNotContain("DOGRULAMA UYARISI", log.Tumu);
    }

    [Fact]
    public async Task Goruntu_alinamamissa_is_BASARILI_ve_uyari_yaziliyor()
    {
        var (sonuc, log) = await CalistirAsync(Okur(Kod, Kod, Kod), goruntuVar: false);

        Assert.True(sonuc.Basarili);
        Assert.Contains("ekran goruntusu alinamamis", log.Tumu);
    }

    // ---- sahteler -----------------------------------------------------------

    private sealed class SahteOkuyucu : IGridOkuyucu
    {
        public GridOkumasi Okuma { get; set; } = GridOkumasi.Bos;
        public int CagriSayisi { get; private set; }

        public Task<GridOkumasi> OkuAsync(string goruntuYolu, CancellationToken ct)
        {
            CagriSayisi++;
            return Task.FromResult(Okuma);
        }
    }

    private sealed class SahteDosyalar : IIsDosyalari
    {
        public string EkstreYolu { get; set; } = "";
        public KodListesi Liste { get; set; } = new();

        public Task<string> EkstreIndirAsync(Guid isId, string klasor, CancellationToken ct)
            => Task.FromResult(EkstreYolu);

        public Task<KodListesi> KodListesiIndirAsync(Guid isId, CancellationToken ct)
            => Task.FromResult(Liste);

        public Task<string?> HataEkraniYukleAsync(string dosyaYolu, CancellationToken ct)
            => Task.FromResult<string?>(null);
    }

    /// <summary>Grid'i dolduran ve AdimMotoru gibi goruntu yolunu birakan surucu.</summary>
    private sealed class SahteSurucu : IOrkaSurucusu
    {
        public string? GoruntuYolu { get; set; }
        public string? SonEkranGoruntusuYolu => null;

        public Task CalistirAsync(OrkaAktarimIstegi istek, GridDoldurVerisi grid,
                                  Action<Adim> adimBasladi, CancellationToken ct)
        {
            for (var i = 0; i < grid.Satirlar.Count; i++)
            {
                grid.YazilanSatir = i + 1;
                grid.SatirYazildi?.Invoke(i + 1, grid.Satirlar.Count);
            }

            grid.GridGoruntusuYolu = GoruntuYolu;
            return Task.CompletedTask;
        }
    }

    private sealed class SessizIlerleme : IIsIlerleme
    {
        public Task BildirAsync(int yuzde, string mesaj, int? tamamlananAdim = null,
                                CancellationToken ct = default) => Task.CompletedTask;
    }
}
