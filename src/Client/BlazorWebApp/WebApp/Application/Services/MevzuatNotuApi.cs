using System.Net;
using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.MevzuatNotlari;

namespace WebApp.Application.Services
{
    public class MevzuatNotuApi : IMevzuatNotuApi
    {
        private readonly HttpClient _http;
        private const string Prefix = "/catalog/mevzuat-notlari";

        public MevzuatNotuApi(HttpClient http) => _http = http;

        public async Task<List<MevzuatNotuDto>> GetAllAsync(string? kategori = null, string? arama = null)
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(kategori)) qs.Add($"kategori={Uri.EscapeDataString(kategori)}");
            if (!string.IsNullOrWhiteSpace(arama)) qs.Add($"arama={Uri.EscapeDataString(arama)}");
            var url = qs.Count > 0 ? $"{Prefix}?{string.Join("&", qs)}" : Prefix;

            return await _http.GetFromJsonAsync<List<MevzuatNotuDto>>(url) ?? new();
        }

        public async Task<Dictionary<string, int>> GetKategoriSayilariAsync()
            => await _http.GetFromJsonAsync<Dictionary<string, int>>($"{Prefix}/kategori-sayilari") ?? new();

        public async Task<MevzuatNotuDto?> GetByIdAsync(int id)
        {
            var resp = await _http.GetAsync($"{Prefix}/{id}");
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<MevzuatNotuDto>();
        }

        public async Task<MevzuatNotuDto?> CreateAsync(MevzuatNotuDto dto)
        {
            var resp = await _http.PostAsJsonAsync(Prefix, dto);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<MevzuatNotuDto>()
                : null;
        }

        public async Task<MevzuatNotuDto?> UpdateAsync(int id, MevzuatNotuDto dto)
        {
            var resp = await _http.PutAsJsonAsync($"{Prefix}/{id}", dto);
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<MevzuatNotuDto>()
                : null;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/{id}");
            return resp.IsSuccessStatusCode;
        }
    }
}
