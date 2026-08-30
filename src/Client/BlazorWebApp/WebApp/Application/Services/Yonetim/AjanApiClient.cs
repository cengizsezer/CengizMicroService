using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class AjanApiClient : IAjanApiClient
    {
        // Gateway kuralları: /auth/admin/{everything} -> IdentityService,
        // /catalog/{everything} -> CatalogService. İkisi de zaten vardı.
        private const string KayitBase = "/auth/admin/agents";
        private const string HubBase = "/catalog/agent";

        private readonly HttpClient _http;
        public AjanApiClient(HttpClient http) => _http = http;

        public async Task<List<AjanDto>> ListeleAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<AjanDto>>(KayitBase, ct) ?? new();

        public async Task<YeniAjanResponse> OlusturAsync(YeniAjanRequest req, CancellationToken ct = default)
        {
            var yanit = await _http.PostAsJsonAsync(KayitBase, req, ct);
            yanit.EnsureSuccessStatusCode();
            return (await yanit.Content.ReadFromJsonAsync<YeniAjanResponse>(cancellationToken: ct))!;
        }

        public async Task IptalEtAsync(int id, string neden, CancellationToken ct = default)
        {
            var yanit = await _http.PostAsJsonAsync($"{KayitBase}/{id}/iptal", new AjanIptalRequest { Neden = neden }, ct);
            yanit.EnsureSuccessStatusCode();

            // İptal edilen ajanın açık soketi hub'da duruyor; token'ının ömrü
            // dolana kadar (8 saat) bağlı kalmasın diye hemen düşürülüyor.
            var dusurme = await _http.PostAsync($"{HubBase}/{id}/dusur", content: null, ct);
            dusurme.EnsureSuccessStatusCode();
        }

        public async Task<List<BagliAjanDto>> BaglilarAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<BagliAjanDto>>($"{HubBase}/baglilar", ct) ?? new();
    }
}
