using System.Net;
using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.SmmmTakip;

namespace WebApp.Application.Services
{
    public class SmmmTakipApi : ISmmmTakipApi
    {
        private readonly HttpClient _http;
        private const string Prefix = "/catalog/smmm-takip";

        public SmmmTakipApi(HttpClient http) => _http = http;

        // ---- Okuma ----

        public async Task<List<SmmmKonuAgacDto>> GetAgacAsync()
            => await _http.GetFromJsonAsync<List<SmmmKonuAgacDto>>($"{Prefix}/agac") ?? new();

        public Task<SmmmKonuDetayDto?> GetKonuBySlugAsync(string slug, int? yil = null)
            => GetOrNull<SmmmKonuDetayDto>($"{Prefix}/konu/{Uri.EscapeDataString(slug)}{YilQuery(yil)}");

        public Task<SmmmKonuDetayDto?> GetKonuByIdAsync(int id, int? yil = null)
            => GetOrNull<SmmmKonuDetayDto>($"{Prefix}/admin/konu/{id}{YilQuery(yil)}");

        public Task<List<SmmmHadDto>?> GetHadlerAsync(int konuId, int? yil = null)
            => GetOrNull<List<SmmmHadDto>>($"{Prefix}/konu/{konuId}/hadler{YilQuery(yil)}");

        // ---- Konu (admin) ----

        public async Task<SmmmKonuDetayDto?> CreateKonuAsync(SmmmKonuSaveDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"{Prefix}/admin/konu", dto);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SmmmKonuDetayDto>() : null;
        }

        public async Task<SmmmKonuDetayDto?> UpdateKonuAsync(int id, SmmmKonuSaveDto dto)
        {
            var resp = await _http.PutAsJsonAsync($"{Prefix}/admin/konu/{id}", dto);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SmmmKonuDetayDto>() : null;
        }

        public async Task<bool> DeleteKonuAsync(int id)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/admin/konu/{id}");
            return resp.IsSuccessStatusCode;
        }

        // ---- Had (admin) ----

        public async Task<SmmmHadDto?> CreateHadAsync(SmmmHadSaveDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"{Prefix}/admin/hadler", dto);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SmmmHadDto>() : null;
        }

        public async Task<SmmmHadDto?> UpdateHadAsync(int id, SmmmHadSaveDto dto)
        {
            var resp = await _http.PutAsJsonAsync($"{Prefix}/admin/hadler/{id}", dto);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SmmmHadDto>() : null;
        }

        public async Task<bool> DeleteHadAsync(int id)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/admin/hadler/{id}");
            return resp.IsSuccessStatusCode;
        }

        // ---- Had değeri (admin) — yıl bazlı upsert ----

        public async Task<SmmmHadDegeriDto?> UpsertHadDegeriAsync(SmmmHadDegeriSaveDto dto)
        {
            var resp = await _http.PutAsJsonAsync($"{Prefix}/admin/had-degeri", dto);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SmmmHadDegeriDto>() : null;
        }

        public async Task<bool> DeleteHadDegeriAsync(int id)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/admin/had-degeri/{id}");
            return resp.IsSuccessStatusCode;
        }

        // ---- Helper ----

        private static string YilQuery(int? yil) => yil.HasValue ? $"?yil={yil.Value}" : string.Empty;

        private async Task<T?> GetOrNull<T>(string url)
        {
            var resp = await _http.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.NotFound) return default;
            if (!resp.IsSuccessStatusCode) return default;
            return await resp.Content.ReadFromJsonAsync<T>();
        }
    }
}
