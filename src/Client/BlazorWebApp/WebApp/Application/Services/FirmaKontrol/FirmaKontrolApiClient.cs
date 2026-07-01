using System.Net.Http.Json;
using WebApp.Application.Services.Yonetim; // ApiErrorParser
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services.FirmaKontrol
{
    public class FirmaKontrolApiClient : IFirmaKontrolApiClient
    {
        private const string Base = "/catalog/firma-kontrol";

        private readonly HttpClient _httpClient;

        public FirmaKontrolApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<FirmaKontrolMaddeDto>> GetMaddelerAsync(int firmaId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{firmaId}/maddeler", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<List<FirmaKontrolMaddeDto>>(cancellationToken: ct)
                   ?? new List<FirmaKontrolMaddeDto>();
        }

        public async Task UpsertMaddeAsync(int firmaId, FirmaKontrolMaddeUpsertDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Base}/{firmaId}/maddeler", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }

        public async Task<FirmaKontrolMaddeDto> AddOzelAsync(int firmaId, OzelMaddeCreateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/{firmaId}/maddeler/ozel", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<FirmaKontrolMaddeDto>(cancellationToken: ct))!;
        }

        public async Task<FirmaKontrolMaddeDto> UpdateOzelAsync(int firmaId, long id, OzelMaddeUpdateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Base}/{firmaId}/maddeler/ozel/{id}", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<FirmaKontrolMaddeDto>(cancellationToken: ct))!;
        }

        public async Task DeleteOzelAsync(int firmaId, long id, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"{Base}/{firmaId}/maddeler/ozel/{id}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }

        // ── Ham mizan ───────────────────────────────────────────────────────

        public async Task<List<FirmaKontrolMizanSatirDto>> GetMizanAsync(int firmaId, int yil, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{firmaId}/mizan?yil={yil}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<List<FirmaKontrolMizanSatirDto>>(cancellationToken: ct)
                   ?? new List<FirmaKontrolMizanSatirDto>();
        }

        public async Task SaveMizanAsync(int firmaId, MizanKaydetRequest req, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/{firmaId}/mizan", req, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }

        public async Task DeleteMizanAsync(int firmaId, int yil, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"{Base}/{firmaId}/mizan?yil={yil}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }

        // ── Vergi paneli girdileri ──────────────────────────────────────────

        public async Task<FirmaKontrolVergiDto?> GetVergiAsync(int firmaId, int donem, int yil, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{firmaId}/vergi?donem={donem}&yil={yil}", ct);

            // 204 = firma için kayıt yok → null döndür.
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return null;

            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<FirmaKontrolVergiDto>(cancellationToken: ct);
        }

        public async Task SaveVergiAsync(int firmaId, FirmaKontrolVergiDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/{firmaId}/vergi", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }
    }
}
