using PkfRobot.Ajan;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>
/// Bagli kalma kurallari: kayit, kalp atisi, ORKA bildirimi, surum reddi ve
/// kopus sonrasi yeniden baglanma.
/// </summary>
public class AjanServisiTests
{
    private static readonly AjanKimlik Kimlik = new()
    {
        MakineId = "BANKA-PC-abc123",
        MakineAdi = "BANKA-PC",
        AjanSurumu = "1.0.0",
        IsletimSistemi = "Windows 11"
    };

    private static (AjanServisi Servis, SahteHubFabrikasi Fabrika, SahteOrka Orka, ListeLog Log)
        Kur(SahteHubFabrikasi? fabrika = null)
    {
        fabrika ??= new SahteHubFabrikasi();

        var log = new ListeLog();
        var orka = new SahteOrka();

        var servis = new AjanServisi(
            fabrika, TokenSaglayici(), orka, Kimlik, "https://ornek/agenthub",
            TimeSpan.FromSeconds(30), log, (_, _) => Task.CompletedTask);

        return (servis, fabrika, orka, log);
    }

    /// <summary>Her istekte gecerli bir token donduren saglayici.</summary>
    private static AjanTokenSaglayici TokenSaglayici()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp();
        for (var i = 0; i < 50; i++)
            http.TokenDondur("T1", saat.SimdiUtc.AddHours(8));

        return new AjanTokenSaglayici(
            new HttpClient(http), "https://ornek/token", () => "pkfr_x0123456789",
            TimeSpan.FromMinutes(30), new ListeLog(),
            (_, _) => Task.CompletedTask, () => saat.SimdiUtc);
    }

    [Fact]
    public async Task Baglaninca_kendini_tanitiyor()
    {
        var (servis, fabrika, orka, _) = Kur();
        orka.Calisiyor = true;

        var kabul = await servis.BaglanVeKaydolAsync();

        Assert.True(kabul);
        Assert.True(servis.Bagli);

        var hub = Assert.Single(fabrika.Uretilenler);
        var kayit = Assert.Single(hub.Kayitlar);
        Assert.Equal("BANKA-PC-abc123", kayit.MakineId);
        Assert.Equal("BANKA-PC", kayit.MakineAdi);
        Assert.Equal("1.0.0", kayit.AjanSurumu);
        Assert.Equal("Windows 11", kayit.IsletimSistemi);
        Assert.True(kayit.OrkaCalisiyorMu);
        Assert.Equal("T1", Assert.Single(fabrika.Tokenlar));
    }

    [Fact]
    public async Task Kalp_atisi_her_turda_gonderiliyor()
    {
        var (servis, fabrika, _, _) = Kur();
        await servis.BaglanVeKaydolAsync();

        await servis.NabizAsync();
        await servis.NabizAsync();
        await servis.NabizAsync();

        Assert.Equal(3, Assert.Single(fabrika.Uretilenler).KalpAtisiSayisi);
    }

    [Fact]
    public async Task Orka_durumu_yalniz_degisimde_bildiriliyor()
    {
        // Her kalp atisinda kayit gondermek, sunucuda 30 saniyede bir gereksiz
        // bir yazma demek olurdu.
        var (servis, fabrika, orka, _) = Kur();
        orka.Calisiyor = false;
        await servis.BaglanVeKaydolAsync();
        var hub = Assert.Single(fabrika.Uretilenler);

        await servis.NabizAsync();
        await servis.NabizAsync();
        Assert.Single(hub.Kayitlar);            // yalniz ilk tanitma

        orka.Calisiyor = true;
        await servis.NabizAsync();
        Assert.Equal(2, hub.Kayitlar.Count);
        Assert.True(hub.Kayitlar[1].OrkaCalisiyorMu);

        await servis.NabizAsync();
        Assert.Equal(2, hub.Kayitlar.Count);    // degisim yok, bildirim de yok

        orka.Calisiyor = false;
        await servis.NabizAsync();
        Assert.Equal(3, hub.Kayitlar.Count);
        Assert.False(hub.Kayitlar[2].OrkaCalisiyorMu);
    }

    [Fact]
    public async Task Orka_kapaliyken_de_bagli_kaliniyor()
    {
        var (servis, fabrika, orka, _) = Kur();
        orka.Calisiyor = false;

        Assert.True(await servis.BaglanVeKaydolAsync());
        await servis.NabizAsync();

        Assert.True(servis.Bagli);
        Assert.False(Assert.Single(fabrika.Uretilenler).Kayitlar[0].OrkaCalisiyorMu);
    }

    [Fact]
    public async Task Eski_surum_reddi_kalici_sayiliyor_ve_mesaj_anlasilir()
    {
        var fabrika = new SahteHubFabrikasi(() => new SahteHub(_ => new KayitSonucu
        {
            Kabul = false,
            Mesaj = "Ajan surumu 1.0.0 desteklenmiyor; en az 2.0.0 gerekiyor.",
            SunucuSurumu = "2.1.0",
            AsgariAjanSurumu = "2.0.0"
        }));

        var (servis, _, _, log) = Kur(fabrika);

        Assert.False(await servis.BaglanVeKaydolAsync());
        Assert.True(servis.KayitKaliciReddedildi);
        Assert.Contains(log.Satirlar, s => s.Contains("desteklenmiyor"));
        Assert.Contains(log.Satirlar, s => s.Contains("2.0.0"));
        Assert.Contains(log.Satirlar, s => s.Contains("guncellenmeden"));
    }

    [Fact]
    public async Task Surum_reddedilince_dongu_yeniden_denemiyor()
    {
        // Guncelleme gerekiyor; sonsuz denemek hem sunucuyu hem log'u dolduruyor.
        var fabrika = new SahteHubFabrikasi(() => new SahteHub(_ => new KayitSonucu
        {
            Kabul = false,
            Mesaj = "Ajan surumu desteklenmiyor.",
            AsgariAjanSurumu = "2.0.0"
        }));

        var bekleme = new BeklemeKaydi();
        var servis = new AjanServisi(
            fabrika, TokenSaglayici(), new SahteOrka(), Kimlik, "https://ornek/agenthub",
            TimeSpan.FromSeconds(30), new ListeLog(), bekleme.Bekle);

        await servis.CalistirAsync(CancellationToken.None);

        Assert.Single(fabrika.Uretilenler);   // tek deneme
        Assert.Empty(bekleme.Sureler);        // geri cekilme yok
    }

    [Fact]
    public async Task Anahtar_gecersizse_dongu_duruyor()
    {
        var http = new SahteHttp().KodDondur(System.Net.HttpStatusCode.Unauthorized);
        var log = new ListeLog();
        var token = new AjanTokenSaglayici(
            new HttpClient(http), "https://ornek/token", () => "pkfr_x0123456789",
            TimeSpan.FromMinutes(30), log, (_, _) => Task.CompletedTask);

        var bekleme = new BeklemeKaydi();
        var servis = new AjanServisi(
            new SahteHubFabrikasi(), token, new SahteOrka(), Kimlik, "https://ornek/agenthub",
            TimeSpan.FromSeconds(30), log, bekleme.Bekle);

        await servis.CalistirAsync(CancellationToken.None);

        Assert.Equal(1, http.IstekSayisi);
        Assert.Empty(bekleme.Sureler);
        Assert.Contains(log.Satirlar, s => s.Contains("Yonetim > Ajanlar"));
    }

    [Fact]
    public async Task Baglanamayan_dongu_geri_cekilerek_deniyor()
    {
        // Ag yok: 5 -> 10 -> 30 diye aciliyor.
        var bekleme = new BeklemeKaydi();
        using var iptal = new CancellationTokenSource();
        var deneme = 0;

        var servis = new AjanServisi(
            new IsleveDayaliFabrika(() => new PatlayanHub()),
            TokenSaglayici(), new SahteOrka(), Kimlik, "https://ornek/agenthub",
            TimeSpan.FromSeconds(30), new ListeLog(),
            (sure, _) =>
            {
                bekleme.Sureler.Add(sure);
                if (++deneme >= 3) iptal.Cancel();
                return Task.CompletedTask;
            });

        await servis.CalistirAsync(iptal.Token);

        Assert.Equal(
            new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) },
            bekleme.Sureler);
    }

    [Fact]
    public async Task Basarili_baglantidan_sonra_geri_cekilme_sifirlaniyor()
    {
        // Baglanti kuruluyor ama hemen kopuyor. Her turda yeniden baglandigi
        // icin aralik buyumemeli: gece bir kez kopmus olmasi, sabahki kopusta
        // bir dakika beklemeyi gerektirmez.
        var bekleme = new BeklemeKaydi();
        using var iptal = new CancellationTokenSource();
        var deneme = 0;

        var fabrika = new IsleveDayaliFabrika(() => new AninaKopanHub());

        var servis = new AjanServisi(
            fabrika, TokenSaglayici(), new SahteOrka(), Kimlik, "https://ornek/agenthub",
            TimeSpan.FromSeconds(30), new ListeLog(),
            (sure, _) =>
            {
                bekleme.Sureler.Add(sure);
                if (++deneme >= 3) iptal.Cancel();
                return Task.CompletedTask;
            });

        await servis.CalistirAsync(iptal.Token);

        Assert.Equal(3, fabrika.Sayi);
        Assert.All(bekleme.Sureler, s => Assert.Equal(TimeSpan.FromSeconds(5), s));
    }

    // --- test icin ozel hub'lar -------------------------------------------

    private sealed class IsleveDayaliFabrika : IHubFabrikasi
    {
        private readonly Func<IHubBaglantisi> _uret;
        public IsleveDayaliFabrika(Func<IHubBaglantisi> uret) => _uret = uret;

        public int Sayi { get; private set; }

        public IHubBaglantisi Olustur(string hubAdresi, string token)
        {
            Sayi++;
            return _uret();
        }
    }

    /// <summary>Baglanti hic kurulamiyor: ag yokmus gibi.</summary>
    private sealed class PatlayanHub : IssizHub
    {
        public override Task BaslatAsync(CancellationToken ct)
            => throw new HttpRequestException("Sunucuya ulasilamiyor.");
    }

    /// <summary>Kayit kabul ediliyor ama soket ayakta kalmiyor.</summary>
    private sealed class AninaKopanHub : IssizHub
    {
        public override Task BaslatAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
