using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class NotApiClient : INotApiClient
    {
        private const string Base = "/catalog/hesapnot";

        private readonly HttpClient _httpClient;

        public NotApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<NotDto>> GetByHesapAsync(int hesapId, int yil, int ay, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{hesapId}?yil={yil}&ay={ay}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<List<NotDto>>(cancellationToken: ct)
                   ?? new List<NotDto>();
        }

        public async Task<NotDto> CreateAsync(NotCreateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync(Base, dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<NotDto>(cancellationToken: ct))!;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"{Base}/{id}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }
    }
}
