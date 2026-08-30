using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PkfRobot.Ajan;

/// <summary>
/// Anahtarin kalici olarak reddedildigi durum: iptal edilmis ya da hic taninmayan
/// anahtar. Tekrar denemenin anlami yok -- insan mudahalesi gerekiyor.
/// </summary>
public class AjanAnahtariGecersizException : Exception
{
    public AjanAnahtariGecersizException(string mesaj) : base(mesaj) { }
}

/// <summary>Gecici sorun: ag yok, sunucu 5xx, hiz siniri asildi. Yeniden denenir.</summary>
public class AjanTokenGeciciHatasi : Exception
{
    public AjanTokenGeciciHatasi(string mesaj) : base(mesaj) { }
}

/// <summary>
/// Anahtari 8 saatlik ajan token'ina cevirir ve token'i bellekte tutar.
///
/// <b>Token diske yazilmiyor.</b> Anahtar zaten diskte (sifreli) duruyor;
/// token'i da yazmak, omru kisa ikinci bir sirri diske koymak olurdu -- kazanci
/// yok, kaybi var.
///
/// <b>Suresi dolmadan once yenileniyor.</b> Token'in bittigi anda yenilemek,
/// tam da o anda hub'a giden cagrinin yetkisiz dusmesi demek. Kalan sure esigin
/// altina indiginde (varsayilan 30 dakika) yenileniyor.
/// </summary>
public sealed class AjanTokenSaglayici
{
    private readonly HttpClient _http;
    private readonly string _tokenUcu;
    private readonly Func<string> _anahtar;
    private readonly TimeSpan _yenilemeEsigi;
    private readonly IAjanLog _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _bekle;
    private readonly Func<DateTime> _simdiUtc;

    private string? _token;
    private DateTime _bitisUtc;
    private string? _kaliciHata;

    public AjanTokenSaglayici(
        HttpClient http,
        string tokenUcu,
        Func<string> anahtar,
        TimeSpan yenilemeEsigi,
        IAjanLog log,
        Func<TimeSpan, CancellationToken, Task>? bekle = null,
        Func<DateTime>? simdiUtc = null)
    {
        _http = http;
        _tokenUcu = tokenUcu;
        _anahtar = anahtar;
        _yenilemeEsigi = yenilemeEsigi;
        _log = log;
        _bekle = bekle ?? ((sure, ct) => Task.Delay(sure, ct));
        _simdiUtc = simdiUtc ?? (() => DateTime.UtcNow);
    }

    /// <summary>Sunucunun bildirdigi bitis ani; henuz token alinmadiysa null.</summary>
    public DateTime? GecerlilikBitisiUtc => _token is null ? null : _bitisUtc;

    public int AjanId { get; private set; }
    public string AjanAdi { get; private set; } = string.Empty;

    /// <summary>Elde tazeligi yeten bir token var mi?</summary>
    public bool TokenTaze => _token is not null && _bitisUtc - _simdiUtc() > _yenilemeEsigi;

    /// <summary>
    /// Gecerli token. Taze degilse yenilenir. Anahtar kalici olarak reddedilmisse
    /// aga hic cikmadan <see cref="AjanAnahtariGecersizException"/> atar.
    /// </summary>
    public async Task<string> TokenAlAsync(CancellationToken ct = default)
    {
        if (_kaliciHata is not null)
            throw new AjanAnahtariGecersizException(_kaliciHata);

        if (TokenTaze) return _token!;

        var yanit = await IsteAsync(ct);

        // 429: sunucu ne kadar bekleyecegimizi soyluyor, ona uyup bir kez daha deniyoruz.
        if (yanit.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var sure = TekrarSuresi(yanit);
            _log.Uyari($"Token ucu hiz sinirina takildi. {sure.TotalSeconds:0} sn beklenip tekrar denenecek.");
            yanit.Dispose();

            await _bekle(sure, ct);
            yanit = await IsteAsync(ct);
        }

        using (yanit)
        {
            if (yanit.StatusCode == HttpStatusCode.Unauthorized)
            {
                _kaliciHata =
                    "Ajan anahtari gecersiz veya iptal edilmis. " +
                    "Yonetim > Ajanlar ekranindan yeni anahtar uretin ve " +
                    "PkfRobot.exe --ajan --anahtari-sifirla ile girin.";

                _log.Hata(_kaliciHata);
                throw new AjanAnahtariGecersizException(_kaliciHata);
            }

            if (!yanit.IsSuccessStatusCode)
                throw new AjanTokenGeciciHatasi(
                    $"Token ucu {(int)yanit.StatusCode} dondu ({_tokenUcu}).");

            var icerik = await yanit.Content.ReadFromJsonAsync<TokenYaniti>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            if (icerik is null || string.IsNullOrWhiteSpace(icerik.Token))
                throw new AjanTokenGeciciHatasi("Token ucu bos yanit verdi.");

            _token = icerik.Token;
            _bitisUtc = icerik.GecerlilikBitisiUtc;
            AjanId = icerik.AjanId;
            AjanAdi = icerik.AjanAdi;

            _log.Bilgi($"Ajan token'i alindi: {icerik.AjanAdi} (#{icerik.AjanId}), " +
                       $"bitis {icerik.GecerlilikBitisiUtc:yyyy-MM-dd HH:mm} UTC.");

            return _token;
        }
    }

    private Task<HttpResponseMessage> IsteAsync(CancellationToken ct)
        => _http.PostAsJsonAsync(_tokenUcu, new { AjanAnahtari = _anahtar() }, ct);

    /// <summary>
    /// <c>Retry-After</c> saniye ya da tarih olarak gelebiliyor; ikisi de okunuyor.
    /// Basligi hic yoksa makul bir alt sinira dusuluyor -- sinira takilmisken
    /// hemen tekrar denemek ayni duvara carpmak olurdu.
    /// </summary>
    private TimeSpan TekrarSuresi(HttpResponseMessage yanit)
    {
        var basligi = yanit.Headers.RetryAfter;

        if (basligi?.Delta is { } delta && delta > TimeSpan.Zero)
            return delta;

        if (basligi?.Date is { } tarih)
        {
            var fark = tarih.UtcDateTime - _simdiUtc();
            if (fark > TimeSpan.Zero) return fark;
        }

        return TimeSpan.FromSeconds(60);
    }

    private sealed class TokenYaniti
    {
        [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
        [JsonPropertyName("gecerlilikBitisiUtc")] public DateTime GecerlilikBitisiUtc { get; set; }
        [JsonPropertyName("ajanId")] public int AjanId { get; set; }
        [JsonPropertyName("ajanAdi")] public string AjanAdi { get; set; } = string.Empty;
    }
}
