using PkfRobot.Ajan;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>
/// Ajanin is alip calistirmasi: baslangic/ilerleme/bitis bildirimleri, ayni anda
/// tek is, iptal ve taninmayan is tipi.
///
/// Sahte calistirici gercekten bir saniye beklemiyor; bekleme disaridan
/// veriliyor, testler saniyelerce surmesin.
/// </summary>
public class IsCalistirmaTests
{
    private static readonly AjanKimlik Kimlik = new()
    {
        MakineId = "BANKA-PC-abc123",
        MakineAdi = "BANKA-PC",
        AjanSurumu = "1.0.0",
        IsletimSistemi = "Windows 11"
    };

    private static AjanTokenSaglayici TokenSaglayici()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp();
        for (var i = 0; i < 20; i++) http.TokenDondur("T1", saat.SimdiUtc.AddHours(8));

        return new AjanTokenSaglayici(
            new HttpClient(http), "https://ornek/token", () => "pkfr_x0123456789",
            TimeSpan.FromMinutes(30), new ListeLog(), (_, _) => Task.CompletedTask, () => saat.SimdiUtc);
    }

    private static (AjanServisi Servis, SahteHubFabrikasi Fabrika, ListeLog Log)
        Kur(IIsCalistirici? calistirici = null, Func<TimeSpan, CancellationToken, Task>? bekle = null)
    {
        var fabrika = new SahteHubFabrikasi();
        var log = new ListeLog();

        var servis = new AjanServisi(
            fabrika, TokenSaglayici(), new SahteOrka(), Kimlik, "https://ornek/agenthub",
            TimeSpan.FromSeconds(30), log,
            bekle ?? ((_, _) => Task.CompletedTask),
            new[] { calistirici ?? new SahteIsCalistirici(log, (_, _) => Task.CompletedTask) });

        return (servis, fabrika, log);
    }

    private static AjanIsPaketi Paket(string tip = SahteIsCalistirici.Tip) => new()
    {
        IsId = Guid.NewGuid(),
        IsTipi = tip,
        FirmaId = 201,
        Yuk = "{}"
    };

    [Fact]
    public async Task Sahte_is_on_adim_ilerleyip_basariyla_bitiyor()
    {
        var (servis, fabrika, _) = Kur();
        await servis.BaglanVeKaydolAsync();
        var hub = Assert.Single(fabrika.Uretilenler);
        var paket = Paket();

        await hub.IsGonderAsync(paket);
        await IsBiteneKadarBekle(hub, paket.IsId);

        Assert.Equal(paket.IsId, Assert.Single(hub.BaslayanIsler));
        Assert.Equal(SahteIsCalistirici.AdimSayisi, hub.Ilerlemeler.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 },
                     hub.Ilerlemeler.Select(i => i.Yuzde));
        Assert.Equal(Enumerable.Range(1, 10), hub.Ilerlemeler.Select(i => i.Adim!.Value));

        var biten = Assert.Single(hub.Bitenler);
        Assert.True(biten.Basarili);
        Assert.Null(biten.Hata);
        Assert.Contains("KaydetBasilmadi", biten.Ozet);
    }

    [Fact]
    public async Task Ilerleme_yuzdeleri_artan_sirada()
    {
        var (servis, fabrika, _) = Kur();
        await servis.BaglanVeKaydolAsync();
        var hub = Assert.Single(fabrika.Uretilenler);
        var paket = Paket();

        await hub.IsGonderAsync(paket);
        await IsBiteneKadarBekle(hub, paket.IsId);

        var yuzdeler = hub.Ilerlemeler.Select(i => i.Yuzde).ToList();
        Assert.Equal(yuzdeler.OrderBy(y => y), yuzdeler);
        Assert.All(hub.Ilerlemeler, i => Assert.Equal(paket.IsId, i.IsId));
    }

    [Fact]
    public async Task Ayni_anda_ikinci_is_reddediliyor()
    {
        // Sunucu da ayni kurali uyguluyor; buradaki, sunucunun yanildigi durumda
        // son savunma.
        var kapi = new TaskCompletionSource();
        var (servis, fabrika, log) = Kur(new BekleyenCalistirici(kapi.Task));
        await servis.BaglanVeKaydolAsync();
        var hub = Assert.Single(fabrika.Uretilenler);

        var birinci = Paket(BekleyenCalistirici.Tip);
        var ikinci = Paket(BekleyenCalistirici.Tip);

        await hub.IsGonderAsync(birinci);
        await hub.IsGonderAsync(ikinci);

        var reddedilen = Assert.Single(hub.Bitenler);
        Assert.Equal(ikinci.IsId, reddedilen.IsId);
        Assert.False(reddedilen.Basarili);
        Assert.Contains("tek is", reddedilen.Hata);
        Assert.Equal(birinci.IsId, servis.CalisanIsId);

        kapi.SetResult();
        await IsBiteneKadarBekle(hub, birinci.IsId);
    }

    [Fact]
    public async Task Taninmayan_is_tipi_reddediliyor_ve_sebebi_bildiriliyor()
    {
        var (servis, fabrika, _) = Kur();
        await servis.BaglanVeKaydolAsync();
        var hub = Assert.Single(fabrika.Uretilenler);
        var paket = Paket("BilinmeyenTip");

        await hub.IsGonderAsync(paket);

        var biten = Assert.Single(hub.Bitenler);
        Assert.False(biten.Basarili);
        Assert.Contains("BilinmeyenTip", biten.Hata);
        Assert.Contains("guncellenmeli", biten.Hata);
        Assert.Empty(hub.BaslayanIsler);
    }

    [Fact]
    public async Task Iptal_gelince_is_duruyor_ve_basarisiz_bildiriliyor()
    {
        var kapi = new TaskCompletionSource();
        var (servis, fabrika, _) = Kur(new BekleyenCalistirici(kapi.Task));
        await servis.BaglanVeKaydolAsync();
        var hub = Assert.Single(fabrika.Uretilenler);
        var paket = Paket(BekleyenCalistirici.Tip);

        await hub.IsGonderAsync(paket);
        await hub.IsIptalAsync(paket.IsId);
        await IsBiteneKadarBekle(hub, paket.IsId);

        var biten = Assert.Single(hub.Bitenler);
        Assert.False(biten.Basarili);
        Assert.Contains("iptal", biten.Hata, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kaydetmeden kontrol", biten.Hata, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Baska_isin_iptali_calisan_isi_durdurmuyor()
    {
        var kapi = new TaskCompletionSource();
        var (servis, fabrika, _) = Kur(new BekleyenCalistirici(kapi.Task));
        await servis.BaglanVeKaydolAsync();
        var hub = Assert.Single(fabrika.Uretilenler);
        var paket = Paket(BekleyenCalistirici.Tip);

        await hub.IsGonderAsync(paket);
        await hub.IsIptalAsync(Guid.NewGuid());

        Assert.Equal(paket.IsId, servis.CalisanIsId);
        Assert.Empty(hub.Bitenler);

        kapi.SetResult();
        await IsBiteneKadarBekle(hub, paket.IsId);
        Assert.True(Assert.Single(hub.Bitenler).Basarili);
    }

    [Fact]
    public async Task Ajan_kapanirken_calisan_is_basarisiz_bildiriliyor()
    {
        // Yoksa is, sunucunun zaman asimina ugratmasina kadar "calisiyor" gorunur.
        var kapi = new TaskCompletionSource();

        // Bu testte dongu gercekten donuyor; bekleme sifir olsaydi is parcacigi
        // bos dongude sikisirdi.
        var (servis, fabrika, _) = Kur(new BekleyenCalistirici(kapi.Task),
                                       (_, ct) => Task.Delay(20, ct));
        using var iptal = new CancellationTokenSource();

        var dongu = servis.CalistirAsync(iptal.Token);
        await KosulBekle(() => fabrika.Uretilenler.Count > 0);
        var hub = fabrika.Uretilenler[0];

        var paket = Paket(BekleyenCalistirici.Tip);
        await hub.IsGonderAsync(paket);
        await KosulBekle(() => servis.CalisanIsId == paket.IsId);

        iptal.Cancel();
        await dongu;

        var biten = Assert.Single(hub.Bitenler);
        Assert.Equal(paket.IsId, biten.IsId);
        Assert.False(biten.Basarili);
        Assert.Contains("kapatildi", biten.Hata);

        kapi.TrySetResult();
    }

    // ---- yardimcilar --------------------------------------------------------

    private static Task IsBiteneKadarBekle(SahteHub hub, Guid isId)
        => KosulBekle(() => hub.Bitenler.Any(b => b.IsId == isId));

    private static async Task KosulBekle(Func<bool> kosul, int enFazlaMs = 5000)
    {
        var bitis = DateTime.UtcNow.AddMilliseconds(enFazlaMs);
        while (DateTime.UtcNow < bitis)
        {
            if (kosul()) return;
            await Task.Delay(10);
        }

        throw new TimeoutException("Beklenen durum olusmadi.");
    }

    /// <summary>Disaridan salinana kadar bekleyen is; es zamanlilik senaryolari icin.</summary>
    private sealed class BekleyenCalistirici : IIsCalistirici
    {
        public const string Tip = "BekleyenIs";

        private readonly Task _kapi;

        public BekleyenCalistirici(Task kapi) => _kapi = kapi;

        public bool Destekliyor(string isTipi) => isTipi == Tip;

        public async Task<IsSonucu> CalistirAsync(AjanIsPaketi paket, IIsIlerleme ilerleme, CancellationToken ct)
        {
            await ilerleme.BildirAsync(10, "basladi", 1, ct);
            await _kapi.WaitAsync(ct);
            return IsSonucu.Basarildi("{}");
        }
    }
}
