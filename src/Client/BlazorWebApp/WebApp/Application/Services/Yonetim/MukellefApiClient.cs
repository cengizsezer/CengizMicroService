using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class MukellefApiClient : IMukellefApiClient
    {
        private const string Base = "/catalog/mukellefler";

        private readonly HttpClient _httpClient;

        public MukellefApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<PagedResult<MukellefDto>> GetPaginatedAsync(
            int page = 1,
            int pageSize = 50,
            string? search = null,
            MukellefDurumu? durum = null,
            CancellationToken ct = default)
        {
            var query = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrWhiteSpace(search))
                query.Add($"search={Uri.EscapeDataString(search)}");
            if (durum.HasValue)
                query.Add($"durum={durum.Value}");

            var url = $"{Base}?{string.Join("&", query)}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<PagedResult<MukellefDto>>(cancellationToken: ct))
                   ?? new PagedResult<MukellefDto> { Page = page, PageSize = pageSize };
        }

        public async Task<MukellefDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<MukellefDto>(cancellationToken: ct);
        }

        public async Task<MukellefDto> CreateAsync(MukellefCreateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync(Base, dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<MukellefDto>(cancellationToken: ct))!;
        }

        public async Task<MukellefDto> UpdateAsync(int id, MukellefUpdateDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Base}/{id}", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<MukellefDto>(cancellationToken: ct))!;
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"{Base}/{id}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }

        public async Task<MukellefImportResultDto> ImportAsync(
            Stream xlsxStream,
            string fileName,
            MukellefImportMode mode,
            CancellationToken ct = default)
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(xlsxStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(streamContent, "file", fileName);

            var url = $"{Base}/import?mode={mode}";
            var response = await _httpClient.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<MukellefImportResultDto>(cancellationToken: ct))!;
        }
    }
}
