using System.Net;
using System.Text;
using PkfRobot.Ajan;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>Yazilan satirlari biriktiren log; anahtarin sizmadigini sinamak icin.</summary>
public sealed class ListeLog : IAjanLog
{
    public List<string> Satirlar { get; } = new();

    public void Bilgi(string mesaj) => Satirlar.Add(AjanLogMaskesi.Maskele(mesaj));
    public void Uyari(string mesaj) => Satirlar.Add(AjanLogMaskesi.Maskele(mesaj));
    public void Hata(string mesaj) => Satirlar.Add(AjanLogMaskesi.Maskele(mesaj));

    public string Tumu => string.Join("\n", Satirlar);
}

/// <summary>
/// Sirayla verilen yanitlari donduren HttpMessageHandler.
///
/// Token ucunun 401/429 davranisi gercek HTTP anlamlariyla sinaniyor: bir arayuz
/// arkasina saklansaydi, sinanan sey kendi soyutlamamiz olurdu.
/// </summary>
public sealed class SahteHttp : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _yanitlar = new();

    public int IstekSayisi { get; private set; }
    public List<string> Govdeler { get; } = new();

    public SahteHttp Sirala(Func<HttpResponseMessage> yanit)
    {
        _yanitlar.Enqueue(yanit);
        return this;
    }

    public SahteHttp TokenDondur(string token, DateTime bitisUtc, int ajanId = 7, string ad = "Ofis Banka PC")
        => Sirala(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"token\":\"{token}\",\"gecerlilikBitisiUtc\":\"{bitisUtc:yyyy-MM-ddTHH:mm:ssZ}\"," +
                $"\"ajanId\":{ajanId},\"ajanAdi\":\"{ad}\"}}",
                Encoding.UTF8, "application/json")
        });

    public SahteHttp KodDondur(HttpStatusCode kod, string? retryAfter = null)
        => Sirala(() =>
        {
            var yanit = new HttpResponseMessage(kod) { Content = new StringContent("") };
            if (retryAfter is not null) yanit.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
            return yanit;
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage istek, CancellationToken ct)
    {
        IstekSayisi++;
        Govdeler.Add(istek.Content is null ? "" : await istek.Content.ReadAsStringAsync(ct));

        if (_yanitlar.Count == 0)
            throw new InvalidOperationException("Sahte HTTP'de siraya konmus yanit kalmadi.");

        return _yanitlar.Dequeue()();
    }
}

/// <summary>Hub yerine gecen sahte: cagrilari sayar, kopus taklit edilebilir.</summary>
public sealed class SahteHub : IHubBaglantisi
{
    private readonly Func<AjanKaydiIstegi, KayitSonucu> _kayitKarari;

    public SahteHub(Func<AjanKaydiIstegi, KayitSonucu>? kayitKarari = null)
        => _kayitKarari = kayitKarari ?? (_ => new KayitSonucu { Kabul = true, Mesaj = "Kayit kabul edildi." });

    public bool Bagli { get; set; }
    public int BaslatmaSayisi { get; private set; }
    public int KalpAtisiSayisi { get; private set; }
    public List<AjanKaydiIstegi> Kayitlar { get; } = new();
    public bool BirakildiMi { get; private set; }

    // ---- is tarafi -------------------------------------------------------
    public List<Guid> BaslayanIsler { get; } = new();
    public List<(Guid IsId, int Yuzde, string? Mesaj, int? Adim)> Ilerlemeler { get; } = new();
    public List<(Guid IsId, bool Basarili, string? Hata, string? Ozet, string? EkranDosyaId)> Bitenler { get; } = new();

    private Func<AjanIsPaketi, Task>? _isGeldi;
    private Func<Guid, Task>? _isIptal;

    /// <summary>Sunucu is gonderiyormus gibi davranir.</summary>
    public Task IsGonderAsync(AjanIsPaketi paket) => _isGeldi?.Invoke(paket) ?? Task.CompletedTask;

    /// <summary>Sunucu iptal bildiriyormus gibi davranir.</summary>
    public Task IsIptalAsync(Guid isId) => _isIptal?.Invoke(isId) ?? Task.CompletedTask;

    public Task BaslatAsync(CancellationToken ct)
    {
        BaslatmaSayisi++;
        Bagli = true;
        return Task.CompletedTask;
    }

    public Task<KayitSonucu> KaydolAsync(AjanKaydiIstegi istek, CancellationToken ct)
    {
        Kayitlar.Add(istek);
        return Task.FromResult(_kayitKarari(istek));
    }

    public Task KalpAtisiAsync(CancellationToken ct)
    {
        if (!Bagli) throw new InvalidOperationException("Baglanti kapali.");
        KalpAtisiSayisi++;
        return Task.CompletedTask;
    }

    public void IsGeldiginde(Func<AjanIsPaketi, Task> eylem) => _isGeldi = eylem;
    public void IsIptalEdildiginde(Func<Guid, Task> eylem) => _isIptal = eylem;

    public Task IsBasladiAsync(Guid isId, CancellationToken ct)
    {
        BaslayanIsler.Add(isId);
        return Task.CompletedTask;
    }

    public Task IsIlerlemeAsync(Guid isId, int yuzde, string? mesaj, int? tamamlananAdim, CancellationToken ct)
    {
        Ilerlemeler.Add((isId, yuzde, mesaj, tamamlananAdim));
        return Task.CompletedTask;
    }

    public Task IsBittiAsync(Guid isId, bool basarili, string? hataMesaji, string? sonucOzetiJson,
                             string? hataEkraniDosyaId, CancellationToken ct)
    {
        Bitenler.Add((isId, basarili, hataMesaji, sonucOzetiJson, hataEkraniDosyaId));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        BirakildiMi = true;
        Bagli = false;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Is tarafini kullanmayan testler icin taban: yalniz baglanma davranisi
/// degistirilerek turetiliyor.
/// </summary>
public abstract class IssizHub : IHubBaglantisi
{
    public virtual bool Bagli => false;
    public abstract Task BaslatAsync(CancellationToken ct);

    public virtual Task<KayitSonucu> KaydolAsync(AjanKaydiIstegi istek, CancellationToken ct)
        => Task.FromResult(new KayitSonucu { Kabul = true });

    public virtual Task KalpAtisiAsync(CancellationToken ct) => Task.CompletedTask;

    public void IsGeldiginde(Func<AjanIsPaketi, Task> eylem) { }
    public void IsIptalEdildiginde(Func<Guid, Task> eylem) { }

    public Task IsBasladiAsync(Guid isId, CancellationToken ct) => Task.CompletedTask;
    public Task IsIlerlemeAsync(Guid isId, int yuzde, string? mesaj, int? tamamlananAdim, CancellationToken ct)
        => Task.CompletedTask;
    public Task IsBittiAsync(Guid isId, bool basarili, string? hataMesaji, string? sonucOzetiJson,
                             string? hataEkraniDosyaId, CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class SahteHubFabrikasi : IHubFabrikasi
{
    private readonly Func<SahteHub> _uret;

    public SahteHubFabrikasi(Func<SahteHub>? uret = null) => _uret = uret ?? (() => new SahteHub());

    public List<SahteHub> Uretilenler { get; } = new();
    public List<string> Tokenlar { get; } = new();

    public IHubBaglantisi Olustur(string hubAdresi, string token)
    {
        Tokenlar.Add(token);
        var hub = _uret();
        Uretilenler.Add(hub);
        return hub;
    }
}

/// <summary>Disaridan surulen ORKA durumu.</summary>
public sealed class SahteOrka : IOrkaDurumu
{
    public bool Calisiyor { get; set; }
    public bool CalisiyorMu() => Calisiyor;
}

/// <summary>Beklemeleri gercekten beklemeden kaydeden yardimci.</summary>
public sealed class BeklemeKaydi
{
    public List<TimeSpan> Sureler { get; } = new();

    public Func<TimeSpan, CancellationToken, Task> Bekle => (sure, _) =>
    {
        Sureler.Add(sure);
        return Task.CompletedTask;
    };
}

/// <summary>Ileri surulebilen saat.</summary>
public sealed class SahteSaat
{
    public DateTime SimdiUtc { get; set; } = new(2026, 8, 30, 9, 0, 0, DateTimeKind.Utc);

    public void Ilerle(TimeSpan sure) => SimdiUtc = SimdiUtc.Add(sure);
}
