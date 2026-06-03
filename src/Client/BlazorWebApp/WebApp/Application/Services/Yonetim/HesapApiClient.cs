using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class HesapApiClient : IHesapApiClient
    {
        private const string Base = "/catalog/hesaplar";

        private readonly HttpClient _httpClient;

        public HesapApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<HesapDto>> GetAllAsync(int? firmaId = null, bool includeInactive = false, CancellationToken ct = default)
        {
            var query = new List<string>
            {
                $"includeInactive={includeInactive.ToString().ToLowerInvariant()}"
            };
            if (firmaId.HasValue)
                query.Add($"firmaId={firmaId.Value}");

            var url = $"{Base}?{string.Join("&", query)}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<List<HesapDto>>(cancellationToken: ct)
                   ?? new List<HesapDto>();
        }

        public async Task<HesapDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<HesapDto>(cancellationToken: ct);
        }

        public async Task<HesapDto> CreateAsync(HesapCreateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync(Base, dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<HesapDto>(cancellationToken: ct))!;
        }

        public async Task<HesapDto> UpdateAsync(int id, HesapUpdateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Base}/{id}", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<HesapDto>(cancellationToken: ct))!;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"{Base}/{id}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }
    }
}
