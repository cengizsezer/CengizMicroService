using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models;
using WebApp.Extensions;
using WebApp.Shared.Dto;
using WebApp.Shared.Dto.Education;

namespace WebApp.Application.Services
{
    public class EducationService : IEducationService
    {
        private readonly HttpClient _httpClient;
        private const string Prefix = "/catalog/education";
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public EducationService(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<PaginatedItemsViewModel<EducationItemDto>> GetAsync(
            int pageIndex = 0, int pageSize = 20, string? q = null, string orderBy = "createdAtDesc")
        {
            var url = $"{Prefix}?p={pageIndex}&ps={pageSize}&orderBy={orderBy}";
            if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q)}";

            return await _httpClient.GetResponseAsync<PaginatedItemsViewModel<EducationItemDto>>(url);
        }

        public async Task<EducationItemDto?> GetByIdAsync(int id)
            => await _httpClient.GetResponseAsync<EducationItemDto>($"{Prefix}/{id}");

        public async Task<(EducationItemDto? Data, string? Error)> CreateAsync(
       CreateEducationItemDto dto, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(dto, JsonOpts);
                Console.WriteLine($"[Education Create] POST {Prefix} body={json}");

                using var resp = await _httpClient.PostAsJsonAsync(Prefix, dto, ct);
                var respText = await resp.Content.ReadAsStringAsync(ct);

                Console.WriteLine($"[Education Create] => {(int)resp.StatusCode} {resp.StatusCode}");
                if (!string.IsNullOrWhiteSpace(respText))
                    Console.WriteLine($"[Education Create] response body: {respText}");

                if (resp.IsSuccessStatusCode)
                {
                    var obj = await resp.Content.ReadFromJsonAsync<EducationItemDto>(cancellationToken: ct);
                    return (obj, null);
                }

                // ProblemDetails veya düz string olabilir – çağırana mesajı verelim
                return (null, string.IsNullOrWhiteSpace(respText) ? resp.StatusCode.ToString() : respText);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Education Create] Exception: {ex}");
                return (null, ex.Message);
            }
        }


        public async Task<EducationItemDto?> UpdateAsync(int id, UpdateEducationItemDto dto, CancellationToken ct = default)
        {
            try
            {
                var url = $"{Prefix}/{id}";
                var json = JsonSerializer.Serialize(dto, JsonOpts);
                Console.WriteLine($"[Education Update] PUT {url} body={json}");

                var resp = await _httpClient.PutAsJsonAsync(url, dto, ct);

                Console.WriteLine($"[Education Update] => {(int)resp.StatusCode} {resp.StatusCode}");
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(body))
                    Console.WriteLine($"[Education Update] response body: {body}");

                if (resp.IsSuccessStatusCode)
                    return await resp.Content.ReadFromJsonAsync<EducationItemDto>(cancellationToken: ct);

                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Education Update] Exception: {ex}");
                return null;
            }
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var url = $"{Prefix}/{id}";
                Console.WriteLine($"[Education Delete] DELETE {url}");

                var resp = await _httpClient.DeleteAsync(url, ct);

                Console.WriteLine($"[Education Delete] => {(int)resp.StatusCode} {resp.StatusCode}");
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(body))
                    Console.WriteLine($"[Education Delete] response body: {body}");

                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Education Delete] Exception: {ex}");
                return false;
            }
        }
    }
}
