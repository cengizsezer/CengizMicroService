using Microsoft.AspNetCore.SignalR.Client;

namespace PkfRobot.Ajan;

/// <summary>Ajanin sunucuya kendini tanittigi paket.</summary>
public class AjanKaydiIstegi
{
    public string MakineId { get; set; } = string.Empty;
    public string MakineAdi { get; set; } = string.Empty;
    public string AjanSurumu { get; set; } = string.Empty;
    public string? IsletimSistemi { get; set; }
    public bool? OrkaCalisiyorMu { get; set; }
}

/// <summary>Sunucunun kayit karari. Reddedilse bile surumleri tasiyor.</summary>
public class KayitSonucu
{
    public bool Kabul { get; set; }
    public string Mesaj { get; set; } = string.Empty;
    public string SunucuSurumu { get; set; } = string.Empty;
    public string AsgariAjanSurumu { get; set; } = string.Empty;
}

/// <summary>
/// Hub baglantisinin arayuzu. SignalR'i arkasina almasinin tek sebebi test:
/// yeniden baglanma, kalp atisi ve ORKA bildirimi kurallari gercek bir soket
/// olmadan sinanabilsin.
/// </summary>
public interface IHubBaglantisi : IAsyncDisposable
{
    bool Bagli { get; }
    Task BaslatAsync(CancellationToken ct);
    Task<KayitSonucu> KaydolAsync(AjanKaydiIstegi istek, CancellationToken ct);
    Task KalpAtisiAsync(CancellationToken ct);

    // ---- sunucudan gelenler ------------------------------------------------
    // Dinleyiciler BaslatAsync'ten ONCE kurulmali: is paketi el sikismanin hemen
    // ardindan gelebiliyor.

    void IsGeldiginde(Func<AjanIsPaketi, Task> eylem);
    void IsIptalEdildiginde(Func<Guid, Task> eylem);

    // ---- ajanin bildirdikleri ----------------------------------------------

    Task IsBasladiAsync(Guid isId, CancellationToken ct);
    Task IsIlerlemeAsync(Guid isId, int yuzde, string? mesaj, int? tamamlananAdim, CancellationToken ct);
    Task IsBittiAsync(Guid isId, bool basarili, string? hataMesaji, string? sonucOzetiJson,
                      string? hataEkraniDosyaId, CancellationToken ct);
}

/// <summary>Her baglanma denemesi icin yeni bir baglanti uretir.</summary>
public interface IHubFabrikasi
{
    IHubBaglantisi Olustur(string hubAdresi, string token);
}

/// <summary>
/// SignalR istemcisinin ince sarmalayicisi.
///
/// <b><c>WithAutomaticReconnect</c> bilerek kullanilmadi:</b> token'in omru 8
/// saat, SignalR'in kendi yeniden baglanmasi ise elindeki token'i yeniler degil,
/// aynen tekrar kullanir. Token bayatladigi anda otomatik yeniden baglanma da
/// susmadan basarisiz olurdu. Yeniden baglanma bu yuzden
/// <see cref="AjanServisi"/>'nde: once token tazeligi, sonra baglanti.
///
/// Token <c>?access_token=</c> ile tasiniyor -- WebSocket el sikismasinda
/// tarayici/istemci <c>Authorization</c> basligi gonderemiyor; sunucu tarafi da
/// bu parametreyi yalniz <c>/agenthub</c> yolunda kabul edecek sekilde
/// yapilandirildi.
/// </summary>
public sealed class SignalRHubBaglantisi : IHubBaglantisi
{
    private readonly HubConnection _baglanti;

    public SignalRHubBaglantisi(string hubAdresi, string token)
    {
        _baglanti = new HubConnectionBuilder()
            .WithUrl(HttpAdresi(hubAdresi), o =>
                o.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .Build();
    }

    public bool Bagli => _baglanti.State == HubConnectionState.Connected;

    public Task BaslatAsync(CancellationToken ct) => _baglanti.StartAsync(ct);

    public Task<KayitSonucu> KaydolAsync(AjanKaydiIstegi istek, CancellationToken ct)
        => _baglanti.InvokeAsync<KayitSonucu>("Kaydol", istek, ct);

    public Task KalpAtisiAsync(CancellationToken ct)
        => _baglanti.InvokeAsync("KalpAtisi", ct);

    public void IsGeldiginde(Func<AjanIsPaketi, Task> eylem)
        => _baglanti.On<AjanIsPaketi>("IsGonder", eylem);

    public void IsIptalEdildiginde(Func<Guid, Task> eylem)
        => _baglanti.On<Guid>("IsIptal", eylem);

    public Task IsBasladiAsync(Guid isId, CancellationToken ct)
        => _baglanti.InvokeAsync("IsBasladi", isId, ct);

    public Task IsIlerlemeAsync(Guid isId, int yuzde, string? mesaj, int? tamamlananAdim, CancellationToken ct)
        => _baglanti.InvokeAsync("IsIlerleme", isId, yuzde, mesaj, tamamlananAdim, ct);

    public Task IsBittiAsync(Guid isId, bool basarili, string? hataMesaji, string? sonucOzetiJson,
                             string? hataEkraniDosyaId, CancellationToken ct)
        => _baglanti.InvokeAsync("IsBitti", isId, basarili, hataMesaji, sonucOzetiJson, hataEkraniDosyaId, ct);

    public ValueTask DisposeAsync() => _baglanti.DisposeAsync();

    /// <summary>
    /// Ayar dosyasina <c>wss://</c> yazilmasi dogal geliyor ama SignalR once
    /// HTTP ile "negotiate" yapiyor ve http/https bekliyor. Sema burada
    /// cevriliyor ki ayar dosyasindaki iki yazim da calissin.
    /// </summary>
    public static string HttpAdresi(string adres)
    {
        if (adres.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            return "https://" + adres[6..];

        if (adres.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            return "http://" + adres[5..];

        return adres;
    }
}

public sealed class SignalRHubFabrikasi : IHubFabrikasi
{
    public IHubBaglantisi Olustur(string hubAdresi, string token)
        => new SignalRHubBaglantisi(hubAdresi, token);
}
