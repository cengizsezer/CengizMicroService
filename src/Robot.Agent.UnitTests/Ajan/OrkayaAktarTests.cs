using ClosedXML.Excel;
using PkfRobot.Ajan;
using PkfRobot.Config;
using PkfRobot.Core;
using System.Text.Json;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>
/// Gercek ORKA aktariminin <b>test edilebilir</b> yani: on dogrulamalar,
/// ilerleme, sonuc ozeti ve hata durumunda ekran goruntusu.
///
/// ORKA'nin kendisi ev makinesinde yok; surucu arayuz arkasinda ve burada
/// sahtesiyle calisiyor. ORKA akisinin dogrulanmasi ofiste, OZET.md'deki
/// kontrol listesiyle yapiliyor.
/// </summary>
public class OrkayaAktarTests : IDisposable
{
    private readonly string _klasor;

    public OrkayaAktarTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-aktar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch { }
    }

    // ---- kurulum ------------------------------------------------------------

    private static OrkayaAktarYuku Yuk(int satirSayisi = 3) => new()
    {
        EkstreYuklemeId = 12,
        FirmaId = 201,
        BankaHesabiOrkaKodu = "102 1 1 01",
        FirmaKodu = "0001",
        SatirSayisi = satirSayisi
    };

    private static KodListesi Liste(int satirSayisi = 3, string kod = "320 A01", string aciklama = "ORNEK ODEME")
        => new()
        {
            EkstreId = 12,
            SatirSayisi = satirSayisi,
            Satirlar = Enumerable.Range(1, satirSayisi)
                .Select(i => new OrkaSatiri
                {
                    SiraNo = i,
                    Aciklama = aciklama,
                    KarsiHesapKodu = kod,
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
        sayfa.Cell(1, 3).Value = "Giren";
        sayfa.Cell(1, 4).Value = "Çıkan";

        for (var i = 0; i < veriSatiri; i++)
        {
            sayfa.Cell(i + 2, 1).Value = new DateTime(2026, 1, i % 28 + 1);
            sayfa.Cell(i + 2, 2).Value = "ORNEK ODEME";
            sayfa.Cell(i + 2, 3).Value = 100 + i;
        }

        kitap.SaveAs(yol);
        return yol;
    }

    private (OrkayaAktarCalistirici Calistirici, SahteIsDosyalari Dosyalar, SahteSurucu Surucu, ListeLog Log)
        Kur(KodListesi? liste = null, int ekstreSatiri = 3, Exception? surucuHatasi = null)
    {
        var log = new ListeLog();
        var dosyalar = new SahteIsDosyalari
        {
            EkstreYolu = Ekstre(ekstreSatiri),
            Liste = liste ?? Liste()
        };
        var surucu = new SahteSurucu { Hata = surucuHatasi, EkranKlasoru = _klasor };

        var calistirici = new OrkayaAktarCalistirici(
            new RobotConfig(), dosyalar, surucu, new SahteOrka(), log,
            Path.Combine(_klasor, "isler"));

        return (calistirici, dosyalar, surucu, log);
    }

    private static AjanIsPaketi Paket(OrkayaAktarYuku yuk) => new()
    {
        IsId = Guid.NewGuid(),
        IsTipi = OrkayaAktarCalistirici.Tip,
        FirmaId = yuk.FirmaId,
        Yuk = JsonSerializer.Serialize(yuk)
    };

    // ---- on dogrulamalar ----------------------------------------------------

    [Fact]
    public void Satir_sayisi_uyusmazliginda_is_baslamiyor()
    {
        // En tehlikeli durum: kodlar bir satir kayarsa her kayit yanlis hesaba gider.
        var hata = Assert.Throws<IsDogrulamaHatasi>(
            () => OrkayaAktarCalistirici.Dogrula(Yuk(3), Liste(2), Ekstre(3)));

        Assert.Contains("Satir sayisi uyusmuyor", hata.Message);
        Assert.Contains("baslatilmadi", hata.Message);
    }

    [Fact]
    public void Ekstre_satir_sayisi_tutmayinca_is_baslamiyor()
    {
        var hata = Assert.Throws<IsDogrulamaHatasi>(
            () => OrkayaAktarCalistirici.Dogrula(Yuk(3), Liste(3), Ekstre(5)));

        Assert.Contains("duzeltilmis ekstre 5", hata.Message);
        Assert.Contains("yanlis satirlara", hata.Message);
    }

    [Fact]
    public void Kod_listesinde_bos_hesap_kodu_varsa_is_baslamiyor()
    {
        var liste = Liste(3);
        liste.Satirlar[1].KarsiHesapKodu = "  ";

        var hata = Assert.Throws<IsDogrulamaHatasi>(
            () => OrkayaAktarCalistirici.Dogrula(Yuk(3), liste, Ekstre(3)));

        Assert.Contains("Satir 2", hata.Message);
        Assert.Contains("karsi hesap kodu bos", hata.Message);
    }

    [Fact]
    public void Kod_listesinde_bos_aciklama_varsa_is_baslamiyor()
    {
        var liste = Liste(3);
        liste.Satirlar[2].Aciklama = "";

        var hata = Assert.Throws<IsDogrulamaHatasi>(
            () => OrkayaAktarCalistirici.Dogrula(Yuk(3), liste, Ekstre(3)));

        Assert.Contains("Satir 3", hata.Message);
        Assert.Contains("aciklama bos", hata.Message);
    }

    [Fact]
    public void Bos_kod_listesi_reddediliyor()
    {
        var hata = Assert.Throws<IsDogrulamaHatasi>(
            () => OrkayaAktarCalistirici.Dogrula(Yuk(0), Liste(0), Ekstre(0)));

        Assert.Contains("Kod listesi bos", hata.Message);
    }

    [Fact]
    public void Dogru_paket_dogrulamadan_geciyor()
        => OrkayaAktarCalistirici.Dogrula(Yuk(3), Liste(3), Ekstre(3));

    [Fact]
    public void Ekstre_satir_sayisi_baslik_haric_sayiliyor()
    {
        Assert.Equal(7, OrkayaAktarCalistirici.EkstreSatirSayisi(Ekstre(7)));
        Assert.Equal(0, OrkayaAktarCalistirici.EkstreSatirSayisi(Ekstre(0)));
    }

    [Fact]
    public void Okunamayan_ekstre_anlasilir_hata_veriyor()
    {
        var bozuk = Path.Combine(_klasor, "bozuk.xlsx");
        File.WriteAllText(bozuk, "bu bir excel dosyasi degil");

        var hata = Assert.Throws<IsDogrulamaHatasi>(() => OrkayaAktarCalistirici.EkstreSatirSayisi(bozuk));

        Assert.Contains("okunamadi", hata.Message);
    }

    // ---- uctan uca (surucu sahte) -------------------------------------------

    [Fact]
    public async Task Basarili_aktarim_ozet_donduruyor_ve_kaydet_basilmadi_diyor()
    {
        var (calistirici, _, surucu, _) = Kur(Liste(3), ekstreSatiri: 3);
        var ilerleme = new IlerlemeKaydi();

        var sonuc = await calistirici.CalistirAsync(Paket(Yuk(3)), ilerleme, CancellationToken.None);

        Assert.True(sonuc.Basarili);
        Assert.Null(sonuc.HataMesaji);

        using var ozet = JsonDocument.Parse(sonuc.SonucOzetiJson!);
        Assert.Equal(3, ozet.RootElement.GetProperty("YazilanSatir").GetInt32());
        Assert.Equal(3, ozet.RootElement.GetProperty("ToplamSatir").GetInt32());
        Assert.True(ozet.RootElement.GetProperty("KaydetBasilmadi").GetBoolean());

        Assert.Equal(3, surucu.YazilanSatir);
    }

    [Fact]
    public async Task Ilerleme_yuzdeleri_artan_sirada_ve_yuzde_yuzde_bitiyor()
    {
        var (calistirici, _, _, _) = Kur(Liste(20), ekstreSatiri: 20);
        var ilerleme = new IlerlemeKaydi();

        await calistirici.CalistirAsync(Paket(Yuk(20)), ilerleme, CancellationToken.None);

        var yuzdeler = ilerleme.Kayitlar.Select(k => k.Yuzde).ToList();
        Assert.NotEmpty(yuzdeler);
        Assert.Equal(yuzdeler.OrderBy(y => y), yuzdeler);
        Assert.Equal(100, yuzdeler[^1]);
        Assert.Contains(ilerleme.Kayitlar, k => k.Mesaj.Contains("Kaydet'e basilmadi"));
    }

    [Fact]
    public async Task Grid_ilerlemesi_elli_ile_doksanbes_arasinda_bildiriliyor()
    {
        var (calistirici, _, _, _) = Kur(Liste(30), ekstreSatiri: 30);
        var ilerleme = new IlerlemeKaydi();

        await calistirici.CalistirAsync(Paket(Yuk(30)), ilerleme, CancellationToken.None);

        var grid = ilerleme.Kayitlar.Where(k => k.Mesaj.Contains("Karsi hesap kodlari")).ToList();
        Assert.NotEmpty(grid);
        Assert.All(grid, k => Assert.InRange(k.Yuzde, 50, 95));
    }

    [Fact]
    public async Task Dosya_indirilemezse_anlasilir_hata_donuyor()
    {
        var (calistirici, dosyalar, _, _) = Kur();
        dosyalar.EkstreHatasi = new IsDogrulamaHatasi(
            "Duzeltilmis ekstre indirilemedi (404). Is bulunamadi.");
        var ilerleme = new IlerlemeKaydi();

        var sonuc = await calistirici.CalistirAsync(Paket(Yuk(3)), ilerleme, CancellationToken.None);

        Assert.False(sonuc.Basarili);
        Assert.Contains("indirilemedi", sonuc.HataMesaji);
        Assert.Null(sonuc.HataEkraniDosyaId);   // ORKA'ya hic dokunulmadi
    }

    [Fact]
    public async Task Dogrulama_tutmazsa_orka_hic_surulmuyor()
    {
        var (calistirici, _, surucu, _) = Kur(Liste(2), ekstreSatiri: 3);
        var ilerleme = new IlerlemeKaydi();

        var sonuc = await calistirici.CalistirAsync(Paket(Yuk(3)), ilerleme, CancellationToken.None);

        Assert.False(sonuc.Basarili);
        Assert.Contains("Satir sayisi uyusmuyor", sonuc.HataMesaji);
        Assert.Equal(0, surucu.CalistirmaSayisi);
    }

    [Fact]
    public async Task Orka_hatasinda_ekran_goruntusu_yukleniyor_ve_uyari_veriliyor()
    {
        File.WriteAllBytes(Path.Combine(_klasor, "adim-07-HATA.png"), new byte[] { 1, 2, 3 });

        var (calistirici, dosyalar, _, _) = Kur(
            surucuHatasi: new InvalidOperationException("Dogrulama basarisiz: 'Veri Transferi' iceren pencere yok."));
        var ilerleme = new IlerlemeKaydi();

        var sonuc = await calistirici.CalistirAsync(Paket(Yuk(3)), ilerleme, CancellationToken.None);

        Assert.False(sonuc.Basarili);
        Assert.Equal("42", sonuc.HataEkraniDosyaId);
        Assert.Single(dosyalar.YuklenenEkranlar);
        Assert.Contains("Veri Transferi", sonuc.HataMesaji);
        Assert.Contains("KAYDETMEDEN", sonuc.HataMesaji);
    }

    [Fact]
    public async Task Bozuk_is_paketi_reddediliyor()
    {
        var (calistirici, _, surucu, _) = Kur();
        var paket = new AjanIsPaketi
        {
            IsId = Guid.NewGuid(),
            IsTipi = OrkayaAktarCalistirici.Tip,
            Yuk = "{\"EkstreYuklemeId\":0}"
        };

        var sonuc = await calistirici.CalistirAsync(paket, new IlerlemeKaydi(), CancellationToken.None);

        Assert.False(sonuc.Basarili);
        Assert.Contains("ekstre kimligi yok", sonuc.HataMesaji);
        Assert.Equal(0, surucu.CalistirmaSayisi);
    }

    [Fact]
    public void Sadece_kendi_is_tipini_destekliyor()
    {
        var (calistirici, _, _, _) = Kur();

        Assert.True(calistirici.Destekliyor("OrkayaAktar"));
        Assert.False(calistirici.Destekliyor("SahteAktarim"));
    }

    // ---- Kaydet'e basmama kurali --------------------------------------------

    [Fact]
    public void Gorev_json_i_kaydet_adimi_icermiyor()
    {
        // Kural pazarlik konusu degil: akis JSON'da durdugu icin bu test onu
        // dosyanin kendisi uzerinden sabitliyor.
        var yol = Path.Combine(AppContext.BaseDirectory, "gorevler", "orkaya-aktar.json");
        Assert.True(File.Exists(yol), $"Gorev dosyasi publish ciktisinda yok: {yol}");

        var gorev = Gorev.Yukle(yol);

        Assert.DoesNotContain(gorev.Adimlar, a =>
            a.Tip.Equals("OnayGerekir", StringComparison.OrdinalIgnoreCase));

        // Log ve EkranGoruntusu hicbir tusa basmiyor; metinlerinde "Kaydet" gecmesi
        // (ornegin "Kaydet'e BASILMAYACAK" notu) kurali bozmuyor.
        var tusaBasanlar = gorev.Adimlar.Where(a =>
            !a.Tip.Equals("Log", StringComparison.OrdinalIgnoreCase) &&
            !a.Tip.Equals("EkranGoruntusu", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(tusaBasanlar, a =>
            a.Deger.Contains("ALT+K", StringComparison.OrdinalIgnoreCase) ||
            a.Deger.Contains("Kaydet", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(gorev.Adimlar, a =>
            a.Tip.Equals("GridDoldur", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Gorev_json_i_ilerleme_kilometre_taslarini_tasiyor()
    {
        var gorev = Gorev.Yukle(Path.Combine(AppContext.BaseDirectory, "gorevler", "orkaya-aktar.json"));

        var yuzdeler = gorev.Adimlar.Where(a => a.Yuzde is not null).Select(a => a.Yuzde!.Value).ToList();

        Assert.Equal(yuzdeler.OrderBy(y => y), yuzdeler);
        Assert.Contains(5, yuzdeler);
        Assert.Contains(50, yuzdeler);
    }

    // ---- sahteler -----------------------------------------------------------

    private sealed class IlerlemeKaydi : IIsIlerleme
    {
        public List<(int Yuzde, string Mesaj, int? Adim)> Kayitlar { get; } = new();

        public Task BildirAsync(int yuzde, string mesaj, int? tamamlananAdim = null, CancellationToken ct = default)
        {
            Kayitlar.Add((yuzde, mesaj, tamamlananAdim));
            return Task.CompletedTask;
        }
    }

    private sealed class SahteIsDosyalari : IIsDosyalari
    {
        public string EkstreYolu { get; set; } = "";
        public KodListesi Liste { get; set; } = new();
        public Exception? EkstreHatasi { get; set; }
        public List<string> YuklenenEkranlar { get; } = new();

        public Task<string> EkstreIndirAsync(Guid isId, string klasor, CancellationToken ct)
            => EkstreHatasi is not null ? Task.FromException<string>(EkstreHatasi) : Task.FromResult(EkstreYolu);

        public Task<KodListesi> KodListesiIndirAsync(Guid isId, CancellationToken ct)
            => Task.FromResult(Liste);

        public Task<string?> HataEkraniYukleAsync(string dosyaYolu, CancellationToken ct)
        {
            YuklenenEkranlar.Add(dosyaYolu);
            return Task.FromResult<string?>("42");
        }
    }

    /// <summary>ORKA yerine gecen surucu: grid satirlarini sayar, istenirse patlar.</summary>
    private sealed class SahteSurucu : IOrkaSurucusu
    {
        public Exception? Hata { get; set; }
        public string? EkranKlasoru { get; set; }
        public int CalistirmaSayisi { get; private set; }
        public int YazilanSatir { get; private set; }

        public string? SonEkranGoruntusuYolu => EkranKlasoru;

        public Task CalistirAsync(OrkaAktarimIstegi istek, GridDoldurVerisi grid,
                                  Action<Adim> adimBasladi, CancellationToken ct)
        {
            CalistirmaSayisi++;

            // Gercek akisin kilometre taslarini taklit et.
            adimBasladi(new Adim { Tip = "OrkaBaslat", Not = "ORKA baslatiliyor", Yuzde = 5 });
            adimBasladi(new Adim { Tip = "Dogrula", Not = "Ekran dogrulaniyor", Yuzde = 45 });

            if (Hata is not null) throw Hata;

            for (var i = 0; i < grid.Satirlar.Count; i++)
            {
                grid.YazilanSatir = i + 1;
                grid.SatirYazildi?.Invoke(i + 1, grid.Satirlar.Count);
            }

            YazilanSatir = grid.YazilanSatir;
            return Task.CompletedTask;
        }
    }
}
