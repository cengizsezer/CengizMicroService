using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class FirmaApiClient : IFirmaApiClient
    {
        private const string Base = "/catalog/firmalar";

        private readonly HttpClient _httpClient;

        public FirmaApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<FirmaDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            var url = $"{Base}?includeInactive={includeInactive.ToString().ToLowerInvariant()}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<List<FirmaDto>>(cancellationToken: ct)
                   ?? new List<FirmaDto>();
        }

        public async Task<FirmaDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<FirmaDto>(cancellationToken: ct);
        }

        public async Task<FirmaDto> CreateAsync(FirmaCreateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync(Base, dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<FirmaDto>(cancellationToken: ct))!;
        }

        public async Task<FirmaDto> UpdateAsync(int id, FirmaUpdateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Base}/{id}", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<FirmaDto>(cancellationToken: ct))!;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"{Base}/{id}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }
    }
}
