using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PkfRobot.Ajan;

/// <summary><c>OrkayaAktar</c> isinin yuku; sunucudaki karsiligiyla ayni alanlar.</summary>
public class OrkayaAktarYuku
{
    public int EkstreYuklemeId { get; set; }
    public int FirmaId { get; set; }
    public string BankaHesabiOrkaKodu { get; set; } = string.Empty;
    public string FirmaKodu { get; set; } = string.Empty;
    public int SatirSayisi { get; set; }
}

/// <summary>Kod listesindeki tek satir (sunucudaki <c>OrkaSatirDto</c>).</summary>
public class OrkaSatiri
{
    public int SiraNo { get; set; }
    public string Aciklama { get; set; } = string.Empty;
    public string KarsiHesapKodu { get; set; } = string.Empty;
    public string? HesapAdi { get; set; }
    public string BankaHesapKodu { get; set; } = string.Empty;
}

/// <summary>Kod listesi yaniti (sunucudaki <c>DisaAktarimSonucDto</c>).</summary>
public class KodListesi
{
    public int EkstreId { get; set; }
    public string DosyaAdi { get; set; } = string.Empty;
    public int SatirSayisi { get; set; }
    public List<OrkaSatiri> Satirlar { get; set; } = new();
}

/// <summary>
/// Isin dosyalarini indirir ve hata ekranini yukler.
///
/// Arayuz olmasinin sebebi test: aktarim calistiricisinin dogrulama kurallari
/// (satir sayisi tutuyor mu, kod bos mu) ag olmadan sinanabilsin.
/// </summary>
public interface IIsDosyalari
{
    /// <summary>Duzeltilmis ekstreyi indirip verilen yola yazar; yazilan yolu doner.</summary>
    Task<string> EkstreIndirAsync(Guid isId, string klasor, CancellationToken ct);

    Task<KodListesi> KodListesiIndirAsync(Guid isId, CancellationToken ct);

    /// <summary>
    /// Hata ekraninin goruntusunu sunucuya yukler ve dosya kimligini doner.
    /// Yuklenemezse null -- ekran goruntusu bir yardimci, isin sonucunu
    /// bildirmeyi engellememeli.
    /// </summary>
    Task<string?> HataEkraniYukleAsync(string dosyaYolu, CancellationToken ct);
}

/// <summary>
/// Gercek uygulama: iki dosya CatalogService'ten, ekran goruntusu
/// FileApiService'e.
///
/// <b>Dosyalar ise bagli uclardan geliyor</b> (<c>/catalog/agent/is/{id}/...</c>),
/// Banka Otomasyon uclarindan degil: ajanin erisebildigi alan o an yapmakta
/// oldugu isten ibaret.
/// </summary>
public sealed class IsDosyalari : IIsDosyalari
{
    private readonly HttpClient _http;
    private readonly Func<CancellationToken, Task<string>> _token;
    private readonly string _catalogKok;
    private readonly string _dosyaYuklemeUcu;
    private readonly IAjanLog _log;

    public IsDosyalari(HttpClient http, Func<CancellationToken, Task<string>> token,
                       string catalogKok, string dosyaYuklemeUcu, IAjanLog log)
    {
        _http = http;
        _token = token;
        _catalogKok = catalogKok.TrimEnd('/');
        _dosyaYuklemeUcu = dosyaYuklemeUcu;
        _log = log;
    }

    public async Task<string> EkstreIndirAsync(Guid isId, string klasor, CancellationToken ct)
    {
        Directory.CreateDirectory(klasor);

        using var yanit = await GonderAsync($"{_catalogKok}/is/{isId}/ekstre", ct);
        await DogrulaAsync(yanit, "Duzeltilmis ekstre", ct);

        var yol = Path.Combine(klasor, $"ekstre-{isId:N}.xlsx");
        await using (var akis = File.Create(yol))
            await yanit.Content.CopyToAsync(akis, ct);

        var boyut = new FileInfo(yol).Length;
        if (boyut < 1024)
            throw new IsDogrulamaHatasi(
                $"Indirilen ekstre dosyasi cok kucuk ({boyut} bayt); bozuk olabilir.");

        _log.Bilgi($"Duzeltilmis ekstre indirildi: {yol} ({boyut / 1024} KB)");
        return yol;
    }

    public async Task<KodListesi> KodListesiIndirAsync(Guid isId, CancellationToken ct)
    {
        using var yanit = await GonderAsync($"{_catalogKok}/is/{isId}/kod-listesi", ct);
        await DogrulaAsync(yanit, "Kod listesi", ct);

        var liste = await yanit.Content.ReadFromJsonAsync<KodListesi>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

        if (liste is null)
            throw new IsDogrulamaHatasi("Kod listesi okunamadi (bos yanit).");

        _log.Bilgi($"Kod listesi indirildi: {liste.Satirlar.Count} satir.");
        return liste;
    }

    public async Task<string?> HataEkraniYukleAsync(string dosyaYolu, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(dosyaYolu)) return null;

            using var icerik = new MultipartFormDataContent();
            var bayt = await File.ReadAllBytesAsync(dosyaYolu, ct);
            var dosya = new ByteArrayContent(bayt);
            dosya.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            icerik.Add(dosya, "file", Path.GetFileName(dosyaYolu));
            icerik.Add(new StringContent("robot-hata"), "folder");

            using var istek = new HttpRequestMessage(HttpMethod.Post, _dosyaYuklemeUcu) { Content = icerik };
            istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _token(ct));

            using var yanit = await _http.SendAsync(istek, ct);
            if (!yanit.IsSuccessStatusCode)
            {
                _log.Uyari($"Hata ekrani yuklenemedi: {(int)yanit.StatusCode}");
                return null;
            }

            var govde = await yanit.Content.ReadAsStringAsync(ct);
            using var belge = JsonDocument.Parse(govde);

            // FileApiService yanitini bir zarf icinde donuyor; kimlik data.id'de.
            if (belge.RootElement.TryGetProperty("data", out var veri) &&
                veri.ValueKind == JsonValueKind.Object &&
                veri.TryGetProperty("id", out var kimlik))
            {
                var id = kimlik.ToString();
                _log.Bilgi($"Hata ekrani yuklendi: dosya #{id}");
                return id;
            }

            _log.Uyari("Hata ekrani yuklendi ama dosya kimligi okunamadi.");
            return null;
        }
        catch (Exception ex)
        {
            // Ekran goruntusu bir yardimci; yuklenememesi isin sonucunu
            // bildirmeyi engellemiyor.
            _log.Uyari($"Hata ekrani yuklenemedi: {ex.Message}");
            return null;
        }
    }

    private async Task<HttpResponseMessage> GonderAsync(string adres, CancellationToken ct)
    {
        using var istek = new HttpRequestMessage(HttpMethod.Get, adres);
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _token(ct));
        return await _http.SendAsync(istek, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static async Task DogrulaAsync(HttpResponseMessage yanit, string ne, CancellationToken ct)
    {
        if (yanit.IsSuccessStatusCode) return;

        var govde = await yanit.Content.ReadAsStringAsync(ct);
        throw new IsDogrulamaHatasi(
            $"{ne} indirilemedi ({(int)yanit.StatusCode}). {Kisalt(govde)}");
    }

    private static string Kisalt(string metin)
        => metin.Length <= 300 ? metin : metin[..300] + "…";
}

/// <summary>
/// Is baslamadan once yapilan dogrulamalardan biri tutmadi. Mesaji kullaniciya
/// oldugu gibi gosteriliyor.
/// </summary>
public class IsDogrulamaHatasi : Exception
{
    public IsDogrulamaHatasi(string mesaj) : base(mesaj) { }
}
