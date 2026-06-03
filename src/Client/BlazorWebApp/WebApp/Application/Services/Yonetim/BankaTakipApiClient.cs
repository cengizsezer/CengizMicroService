using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class BankaTakipApiClient : IBankaTakipApiClient
    {
        private const string Base = "/catalog/bankatakip";

        private readonly HttpClient _httpClient;

        public BankaTakipApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<HesapTakipDto>> GetAyAsync(int year, int month, int? firmaId = null, CancellationToken ct = default)
        {
            var query = new List<string>
            {
                $"year={year}",
                $"month={month}"
            };
            if (firmaId.HasValue)
                query.Add($"firmaId={firmaId.Value}");

            var url = $"{Base}?{string.Join("&", query)}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<List<HesapTakipDto>>(cancellationToken: ct)
                   ?? new List<HesapTakipDto>();
        }

        public async Task<IslemKaydiDto> IsaretleAsync(IsaretleRequestDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/isaretle", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<IslemKaydiDto>(cancellationToken: ct))!;
        }
    }
}
