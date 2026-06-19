using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.TicaretSicil;

namespace WebApp.Application.Services
{
    public class TicaretSicilApi : ITicaretSicilApi
    {
        private readonly HttpClient _http;
        private const string Prefix = "/catalog/ticaret-sicil";

        public TicaretSicilApi(HttpClient http) => _http = http;

        // ---- Okuma ----

        public async Task<List<TicaretSicilIslemListDto>> GetAllAsync()
            => await _http.GetFromJsonAsync<List<TicaretSicilIslemListDto>>(Prefix) ?? new();

        public Task<TicaretSicilIslemDetayDto?> GetBySlugAsync(string slug)
            => GetOrNull<TicaretSicilIslemDetayDto>($"{Prefix}/{Uri.EscapeDataString(slug)}");

        public Task<TicaretSicilIslemDetayDto?> GetByIdAsync(int id)
            => GetOrNull<TicaretSicilIslemDetayDto>($"{Prefix}/admin/{id}");

        // ---- İşlem (admin) ----

        public async Task<TicaretSicilIslemDetayDto?> CreateIslemAsync(TicaretSicilIslemSaveDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"{Prefix}/admin", dto);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<TicaretSicilIslemDetayDto>()
                : null;
        }

        public async Task<TicaretSicilIslemDetayDto?> UpdateIslemAsync(int id, TicaretSicilIslemSaveDto dto)
        {
            var resp = await _http.PutAsJsonAsync($"{Prefix}/admin/{id}", dto);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<TicaretSicilIslemDetayDto>()
                : null;
        }

        public async Task<List<int>?> DeleteIslemAsync(int id)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/admin/{id}");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<int>>()
                : null;
        }

        // ---- Adım (admin) ----

        public async Task<TicaretSicilAdimDto?> AddAdimAsync(int islemId, TicaretSicilAdimSaveDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"{Prefix}/admin/{islemId}/adimlar", dto);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<TicaretSicilAdimDto>()
                : null;
        }

        public async Task<TicaretSicilAdimDto?> UpdateAdimAsync(int adimId, TicaretSicilAdimSaveDto dto)
        {
            var resp = await _http.PutAsJsonAsync($"{Prefix}/admin/adimlar/{adimId}", dto);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<TicaretSicilAdimDto>()
                : null;
        }

        public async Task<List<int>?> DeleteAdimAsync(int adimId)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/admin/adimlar/{adimId}");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<int>>()
                : null;
        }

        public async Task<bool> MoveAdimAsync(int adimId, string direction)
        {
            var resp = await _http.PostAsync($"{Prefix}/admin/adimlar/{adimId}/move?direction={Uri.EscapeDataString(direction)}", null);
            return resp.IsSuccessStatusCode;
        }

        // ---- Ek (admin) ----

        public async Task<TicaretSicilEkDto?> AddEkAsync(int adimId, TicaretSicilEkSaveDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"{Prefix}/admin/adimlar/{adimId}/ekler", dto);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<TicaretSicilEkDto>()
                : null;
        }

        public async Task<int?> DeleteEkAsync(int ekId)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/admin/ekler/{ekId}");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<int>()
                : null;
        }

        // ---- Helper ----

        private async Task<T?> GetOrNull<T>(string url)
        {
            var resp = await _http.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.NotFound) return default;
            if (!resp.IsSuccessStatusCode) return default;
            return await resp.Content.ReadFromJsonAsync<T>();
        }
    }
}
