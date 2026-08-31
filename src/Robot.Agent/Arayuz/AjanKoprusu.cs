using System.Runtime.Versioning;
using PkfRobot.Ajan;
using PkfRobot.Config;

namespace PkfRobot.Arayuz;

public enum BaglantiDurumu
{
    Kapali,
    Baglaniyor,
    Bagli,
    Kopuk
}

/// <summary>
/// Arayuz ile ajan arasindaki kopru: ajani arka planda baslatir, durdurur ve
/// durumunu okur.
///
/// Ajan <b>ayni</b> <see cref="AjanCalistirici"/> ile calisiyor; burada ikinci
/// bir baglanti mantigi yok. Fark yalniz kancalar: log ekrana da dusuyor, isler
/// izleniyor ve anahtar konsol yerine bir pencereden soruluyor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AjanKoprusu : IDisposable
{
    private readonly RobotConfig _cfg;
    private readonly CiftYonluLog _log;
    private readonly IsIzleyici _izleyici;
    private readonly Func<string, string?> _anahtarSor;

    private CancellationTokenSource? _iptal;
    private Task<int>? _calisan;
    private AjanServisi? _servis;

    public AjanKoprusu(RobotConfig cfg, CiftYonluLog log, IsIzleyici izleyici,
                       Func<string, string?> anahtarSor)
    {
        _cfg = cfg;
        _log = log;
        _izleyici = izleyici;
        _anahtarSor = anahtarSor;
    }

    /// <summary>Durum degisti; ekran tazelensin.</summary>
    public event Action? Degisti;

    public bool Calisiyor => _calisan is { IsCompleted: false };

    /// <summary>Ajan durdugunda sunucunun verdigi cikis kodu; henuz durmadiysa null.</summary>
    public int? SonCikisKodu { get; private set; }

    public BaglantiDurumu Durum
    {
        get
        {
            if (!Calisiyor) return BaglantiDurumu.Kapali;
            if (_servis is null) return BaglantiDurumu.Baglaniyor;
            return _servis.Bagli ? BaglantiDurumu.Bagli : BaglantiDurumu.Kopuk;
        }
    }

    /// <summary>Son basarili nabiz; hic baglanilmadiysa null.</summary>
    public DateTime? SonKalpAtisi => _servis?.SonKalpAtisi;

    public Guid CalisanIsId => _servis?.CalisanIsId ?? Guid.Empty;

    public void Baslat()
    {
        if (Calisiyor) return;

        SonCikisKodu = null;
        _iptal = new CancellationTokenSource();

        // Jeton yerel degiskene aliniyor: Durdur() kaynagi birakinca arka plan
        // gorevi ondan token okumaya calisirsa ObjectDisposedException alirdi.
        var jeton = _iptal.Token;

        var kancalar = new AjanKancalari
        {
            // Ajanin actigi gunluk dosyasini ekrana giden kopyanin arkasina
            // takiyoruz; ikinci bir dosya acilmiyor.
            LogSarmala = dosyaLog => { _log.Ic = dosyaLog; return _log; },
            IsSarmala = ic => new IzlenenCalistirici(ic, _izleyici),
            AnahtarSor = kok => _anahtarSor(kok),
            ServisHazir = servis =>
            {
                _servis = servis;
                Degisti?.Invoke();
            }
        };

        // Ajan kendi is parcaciginda: arayuz donmemeli.
        _calisan = Task.Run(async () =>
        {
            try
            {
                return await AjanCalistirici.CalistirAsync(_cfg, false, jeton, kancalar);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                _log.Hata($"Ajan beklenmedik sekilde durdu: {ex.Message}");
                return 1;
            }
            finally
            {
                _servis = null;
                Degisti?.Invoke();
            }
        });

        _ = _calisan.ContinueWith(t =>
        {
            SonCikisKodu = t.Status == TaskStatus.RanToCompletion ? t.Result : 1;
            Degisti?.Invoke();
        }, TaskScheduler.Default);

        Degisti?.Invoke();
    }

    /// <summary>
    /// Ajani durdurur ve kapanmasini bekler.
    ///
    /// Beklemenin sebebi <see cref="AjanServisi"/>'nin kapanirken calisan isi
    /// sunucuya bildirmesi: sureci aniden birakmak, sunucuda zaman asimina kadar
    /// "calisiyor" gorunecek bir is birakirdi.
    /// </summary>
    public void Durdur(TimeSpan? beklemeSuresi = null)
    {
        if (_iptal is null) return;

        _iptal.Cancel();

        try
        {
            _calisan?.Wait(beklemeSuresi ?? TimeSpan.FromSeconds(8));
        }
        catch (AggregateException)
        {
            // Iptal sirasinda cikan hata onemli degil; zaten birakiyoruz.
        }

        _iptal.Dispose();
        _iptal = null;
        _servis = null;

        Degisti?.Invoke();
    }

    /// <summary>Zamanlayicidan cagriliyor; gecen sureler ekranda ilerlesin.</summary>
    public void DurumuTazele() => Degisti?.Invoke();

    public void Dispose() => Durdur(TimeSpan.FromSeconds(3));
}
