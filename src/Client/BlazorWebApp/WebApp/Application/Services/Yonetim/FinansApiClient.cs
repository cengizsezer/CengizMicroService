using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class FinansApiClient : IFinansApiClient
    {
        private const string Base = "/catalog/finans";

        private readonly HttpClient _httpClient;

        public FinansApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<PagedResult<MukellefFinansDto>> GetPaginatedAsync(
            int page = 1,
            int pageSize = 50,
            string? search = null,
            FinansSinifi? finansSinifi = null,
            CancellationToken ct = default)
        {
            var query = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrWhiteSpace(search))
                query.Add($"search={Uri.EscapeDataString(search)}");
            if (finansSinifi.HasValue)
                query.Add($"finansSinifi={finansSinifi.Value}");

            var url = $"{Base}?{string.Join("&", query)}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<PagedResult<MukellefFinansDto>>(cancellationToken: ct))
                   ?? new PagedResult<MukellefFinansDto> { Page = page, PageSize = pageSize };
        }

        public async Task<MukellefFinansDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<MukellefFinansDto>(cancellationToken: ct);
        }

        public async Task<MukellefFinansDto> UpdateFinansAsync(int id, FinansUpdateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Base}/{id}", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<MukellefFinansDto>(cancellationToken: ct))!;
        }
    }
}
