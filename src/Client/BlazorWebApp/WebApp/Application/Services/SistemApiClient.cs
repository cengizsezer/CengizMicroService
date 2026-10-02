using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Shared.Dto.Sistemler;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Kullanılan sistemler: ortak liste (Yönetim → Sistemler ve kartın "+ Yeni ekle"si) ve
    /// firmanın kart formu. Yazma metotları hatada kullanıcıya gösterilecek mesajı döner.
    /// </summary>
    public interface ISistemApiClient
    {
        Task<List<SistemDto>?> ListeAsync(SistemTuru? tur, bool pasiflerDahil, CancellationToken ct = default);

        /// <summary>Sunucu benzer ad bulursa (409) <see cref="SistemYazmaSonucu.Benzer"/> dolu döner — kullanıcıya sorulur.</summary>
        Task<SistemYazmaSonucu> EkleAsync(SistemEkleDto dto, CancellationToken ct = default);

        Task<SistemYazmaSonucu> GuncelleAsync(int id, SistemGuncelleDto dto, CancellationToken ct = default);

        Task<(SistemBirlestirmeSonucuDto? Sonuc, string? Hata)> BirlestirAsync(int kaynakId, int hedefId, CancellationToken ct = default);

        Task<string?> FirmaSistemleriKaydetAsync(int firmaId, FirmaSistemleriKaydetDto dto, CancellationToken ct = default);
    }

    /// <inheritdoc cref="ISistemApiClient"/>
    public class SistemApiClient : ISistemApiClient
    {
        private const string Prefix = "/catalog/sistemler";
        private const string SunucuYok = "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public SistemApiClient(HttpClient http) => _http = http;

        public async Task<List<SistemDto>?> ListeAsync(SistemTuru? tur, bool pasiflerDahil, CancellationToken ct = default)
        {
            var sorgu = $"{Prefix}?pasiflerDahil={(pasiflerDahil ? "true" : "false")}" + (tur is { } t ? $"&tur={(int)t}" : "");
            try
            {
                return await _http.GetFromJsonAsync<List<SistemDto>>(sorgu, Json, ct);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task<SistemYazmaSonucu> EkleAsync(SistemEkleDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsJsonAsync(Prefix, dto, ct), ct);

        public Task<SistemYazmaSonucu> GuncelleAsync(int id, SistemGuncelleDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Prefix}/{id}", dto, ct), ct);

        public async Task<(SistemBirlestirmeSonucuDto? Sonuc, string? Hata)> BirlestirAsync(int kaynakId, int hedefId,
                                                                                         CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.PostAsJsonAsync($"{Prefix}/{kaynakId}/birlestir", new SistemBirlestirDto { HedefId = hedefId }, ct);
                var govde = await resp.Content.ReadAsStringAsync(ct);
                return resp.IsSuccessStatusCode
                    ? (JsonSerializer.Deserialize<SistemBirlestirmeSonucuDto>(govde, Json), null)
                    : (null, MesajCoz(govde) ?? "Birleştirilemedi.");
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        public async Task<string?> FirmaSistemleriKaydetAsync(int firmaId, FirmaSistemleriKaydetDto dto, CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.PutAsJsonAsync($"/catalog/anasayfa/firmalar/{firmaId}/sistemler", dto, ct);
                return resp.IsSuccessStatusCode ? null : MesajCoz(await resp.Content.ReadAsStringAsync(ct)) ?? "Kayıt yapılamadı.";
            }
            catch (Exception)
            {
                return SunucuYok;
            }
        }

        private static async Task<SistemYazmaSonucu> YazAsync(Func<Task<HttpResponseMessage>> istek, CancellationToken ct)
        {
            try
            {
                using var resp = await istek();
                var govde = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                    return new(JsonSerializer.Deserialize<SistemDto>(govde, Json), null, null);

                if (resp.StatusCode == HttpStatusCode.Conflict
                    && JsonSerializer.Deserialize<SistemBenzerDto>(govde, Json) is { } benzer)
                    return new(null, benzer.Benzer, benzer.Message);

                return new(null, null, MesajCoz(govde) ?? "Kayıt yapılamadı.");
            }
            catch (Exception)
            {
                return new(null, null, SunucuYok);
            }
        }

        private static string? MesajCoz(string govde)
        {
            try
            {
                using var belge = JsonDocument.Parse(govde);
                return belge.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
