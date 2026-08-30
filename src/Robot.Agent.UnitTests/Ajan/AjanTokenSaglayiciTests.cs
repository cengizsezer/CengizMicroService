using System.Net;
using PkfRobot.Ajan;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>
/// Token alma kurallari: erken yenileme, kalici ret ve hiz sinirina uyma.
///
/// Bunlarin hepsi ofiste, gece yarisi, kimse bakmazken calisacak seyler; tek
/// dogrulama yolu test.
/// </summary>
public class AjanTokenSaglayiciTests
{
    private const string Anahtar = "pkfr_deneme_anahtari_0123456789";
    private const string Uc = "https://ornek/auth/agent/token";

    private static (AjanTokenSaglayici Saglayici, SahteHttp Http, ListeLog Log, BeklemeKaydi Bekleme)
        Kur(SahteHttp http, SahteSaat saat, int yenilemeEsigiDakika = 30)
    {
        var log = new ListeLog();
        var bekleme = new BeklemeKaydi();
        var saglayici = new AjanTokenSaglayici(
            new HttpClient(http),
            Uc,
            () => Anahtar,
            TimeSpan.FromMinutes(yenilemeEsigiDakika),
            log,
            bekleme.Bekle,
            () => saat.SimdiUtc);

        return (saglayici, http, log, bekleme);
    }

    [Fact]
    public async Task Ilk_cagrida_token_aliniyor()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp().TokenDondur("T1", saat.SimdiUtc.AddHours(8));
        var (saglayici, _, _, _) = Kur(http, saat);

        var token = await saglayici.TokenAlAsync();

        Assert.Equal("T1", token);
        Assert.Equal(1, http.IstekSayisi);
        Assert.Equal(7, saglayici.AjanId);
        Assert.Contains(Anahtar, http.Govdeler[0]);
    }

    [Fact]
    public async Task Taze_token_varken_aga_cikilmiyor()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp().TokenDondur("T1", saat.SimdiUtc.AddHours(8));
        var (saglayici, _, _, _) = Kur(http, saat);

        await saglayici.TokenAlAsync();
        saat.Ilerle(TimeSpan.FromHours(2));
        var ikinci = await saglayici.TokenAlAsync();

        Assert.Equal("T1", ikinci);
        Assert.Equal(1, http.IstekSayisi);
    }

    [Fact]
    public async Task Suresi_dolmak_uzereyken_yenileniyor()
    {
        // Asil kural: token bittigi anda degil, bitmeden ONCE yenilenmeli.
        // Yoksa yenileme tam da hub'a cagri yapilan ana denk gelir.
        var saat = new SahteSaat();
        var http = new SahteHttp()
            .TokenDondur("T1", saat.SimdiUtc.AddHours(8))
            .TokenDondur("T2", saat.SimdiUtc.AddHours(16));
        var (saglayici, _, _, _) = Kur(http, saat, yenilemeEsigiDakika: 30);

        await saglayici.TokenAlAsync();

        // Kalan sure 29 dakika: esigin altinda.
        saat.Ilerle(TimeSpan.FromHours(8) - TimeSpan.FromMinutes(29));
        Assert.False(saglayici.TokenTaze);

        var yeni = await saglayici.TokenAlAsync();

        Assert.Equal("T2", yeni);
        Assert.Equal(2, http.IstekSayisi);
    }

    [Fact]
    public async Task Esik_asilmadan_yenilenmiyor()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp().TokenDondur("T1", saat.SimdiUtc.AddHours(8));
        var (saglayici, _, _, _) = Kur(http, saat, yenilemeEsigiDakika: 30);

        await saglayici.TokenAlAsync();

        // Kalan sure 31 dakika: hala esigin ustunde.
        saat.Ilerle(TimeSpan.FromHours(8) - TimeSpan.FromMinutes(31));

        Assert.True(saglayici.TokenTaze);
        Assert.Equal("T1", await saglayici.TokenAlAsync());
        Assert.Equal(1, http.IstekSayisi);
    }

    [Fact]
    public async Task Dort_yuz_bir_alinca_yeniden_denenmiyor_ve_mesaj_anlasilir()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp().KodDondur(HttpStatusCode.Unauthorized);
        var (saglayici, _, log, _) = Kur(http, saat);

        var hata = await Assert.ThrowsAsync<AjanAnahtariGecersizException>(
            () => saglayici.TokenAlAsync());

        Assert.Contains("iptal edilmis", hata.Message);
        Assert.Contains("Yonetim > Ajanlar", hata.Message);
        Assert.Contains("--anahtari-sifirla", hata.Message);

        // Ikinci cagri aga hic cikmamali: sonsuz donguye girmenin anlami yok.
        await Assert.ThrowsAsync<AjanAnahtariGecersizException>(() => saglayici.TokenAlAsync());
        Assert.Equal(1, http.IstekSayisi);
        Assert.Contains(log.Satirlar, s => s.Contains("Ajan anahtari gecersiz"));
    }

    [Fact]
    public async Task Dort_yuz_yirmi_dokuz_alinca_retry_after_kadar_bekleniyor()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp()
            .KodDondur(HttpStatusCode.TooManyRequests, retryAfter: "45")
            .TokenDondur("T1", saat.SimdiUtc.AddHours(8));
        var (saglayici, _, _, bekleme) = Kur(http, saat);

        var token = await saglayici.TokenAlAsync();

        Assert.Equal("T1", token);
        Assert.Equal(TimeSpan.FromSeconds(45), Assert.Single(bekleme.Sureler));
        Assert.Equal(2, http.IstekSayisi);
    }

    [Fact]
    public async Task Retry_after_yoksa_makul_bir_sure_bekleniyor()
    {
        // Sinira takilmisken hemen tekrar denemek ayni duvara carpmak olurdu.
        var saat = new SahteSaat();
        var http = new SahteHttp()
            .KodDondur(HttpStatusCode.TooManyRequests)
            .TokenDondur("T1", saat.SimdiUtc.AddHours(8));
        var (saglayici, _, _, bekleme) = Kur(http, saat);

        await saglayici.TokenAlAsync();

        Assert.Equal(TimeSpan.FromSeconds(60), Assert.Single(bekleme.Sureler));
    }

    [Fact]
    public async Task Sunucu_hatasi_gecici_sayiliyor()
    {
        // 500 kalici bir ret degil: disaridaki dongu geri cekilip tekrar denesin.
        var saat = new SahteSaat();
        var http = new SahteHttp().KodDondur(HttpStatusCode.InternalServerError);
        var (saglayici, _, _, _) = Kur(http, saat);

        await Assert.ThrowsAsync<AjanTokenGeciciHatasi>(() => saglayici.TokenAlAsync());
    }

    [Fact]
    public async Task Anahtar_log_satirlarina_dusmuyor()
    {
        var saat = new SahteSaat();
        var http = new SahteHttp()
            .KodDondur(HttpStatusCode.TooManyRequests, retryAfter: "10")
            .TokenDondur("eyJhbGciOi.govde.imza", saat.SimdiUtc.AddHours(8));
        var (saglayici, _, log, _) = Kur(http, saat);

        await saglayici.TokenAlAsync();

        Assert.NotEmpty(log.Satirlar);
        Assert.DoesNotContain(Anahtar, log.Tumu);
        Assert.DoesNotContain("eyJhbGciOi.govde.imza", log.Tumu);
    }
}
