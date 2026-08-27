using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Pages.Hesaplamalar.FinansmanGiderKisitlamasi.Model;

namespace WebApp.Pages.Hesaplamalar.FinansmanGiderKisitlamasi.Services
{
    public class FinansmanKisitlamaApiService : IFinansmanKisitlamaApiService
    {
        // Gateway'in genel catalog rotasından geçer (/catalog/{everything} -> Bearer'lı).
        private const string Base = "/catalog/finansman-gider-kisitlamasi";

        private readonly HttpClient _http;

        public FinansmanKisitlamaApiService(HttpClient http) => _http = http;

        public async Task<(FinansmanKisitlamaSonucDto? Sonuc, string? HataMesaji)> HesaplaAsync(
            FinansmanKisitlamaHesapRequest request, CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync($"{Base}/hesapla", request, ct);

            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<FinansmanKisitlamaSonucDto>(cancellationToken: ct), null);

            return (null, await HataMesajiAsync(resp, ct));
        }

        public async Task<List<FinansmanKisitlamaOraniDto>> GetOranlarAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<FinansmanKisitlamaOraniDto>>($"{Base}/oranlar", ct) ?? new();

        public async Task<(FinansmanKisitlamaOraniDto? Oran, string? HataMesaji)> UpsertOranAsync(
            int yil, FinansmanKisitlamaOraniSaveDto dto, CancellationToken ct = default)
        {
            var resp = await _http.PutAsJsonAsync($"{Base}/oranlar/{yil}", dto, ct);

            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<FinansmanKisitlamaOraniDto>(cancellationToken: ct), null);

            return (null, await HataMesajiAsync(resp, ct));
        }

        public async Task<bool> DeleteOranAsync(int yil, CancellationToken ct = default)
        {
            var resp = await _http.DeleteAsync($"{Base}/oranlar/{yil}", ct);
            return resp.IsSuccessStatusCode;
        }

        /// <summary>
        /// Sunucunun <c>{ message }</c> gövdesini çıkarır. Gövde beklenen biçimde değilse
        /// ham metin döner; kullanıcı hiç değilse durum kodundan fazlasını görsün.
        /// </summary>
        private static async Task<string> HataMesajiAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            var govde = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(govde))
                return $"İstek başarısız ({(int)resp.StatusCode}).";

            try
            {
                using var doc = JsonDocument.Parse(govde);
                if (doc.RootElement.TryGetProperty("message", out var mesaj))
                    return mesaj.GetString() ?? govde;
            }
            catch (JsonException)
            {
                // JSON değilse ham metni göster.
            }

            return govde;
        }
    }
}
