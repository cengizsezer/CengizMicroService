using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Extensions;
using WebApp.Shared.Dto.Declaration;
using System.Net.Http.Headers;

namespace WebApp.Application.Services
{
    public class FileApiService : IFileApiService
    {
        private readonly HttpClient _http;
        // İstersen BaseAddress kullan: _http.BaseAddress = new Uri("http://localhost:5009");
        private const string Base = "http://localhost:5009/api/file/v1";

        public FileApiService(HttpClient http) => _http = http;

        public async Task<bool> UploadAsync(
            IBrowserFile file, string companyId, int year, int month,
            string declType, string docType, CancellationToken ct = default)
        {
            using var content = new MultipartFormDataContent();

            // Dosya
            var stream = file.OpenReadStream(20 * 1024 * 1024, ct); // 20MB
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            content.Add(fileContent, "file", file.Name);

            // Form alanları
            content.Add(new StringContent(companyId), "companyId");
            content.Add(new StringContent(year.ToString()), "year");
            content.Add(new StringContent(month.ToString()), "month");
            content.Add(new StringContent(declType), "declType");
            content.Add(new StringContent(docType), "docType");

            using var resp = await _http.PostAsync($"{Base}/upload", content, ct);
            var bodyStr = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode) return false;

            var env = JsonConvert.DeserializeObject<HttpDataResponse<bool>>(bodyStr);
            return env?.Data == true;
        }

        //public Task<List<FileInfoDto>?> ListAsync(string companyId, int year, int month, CancellationToken ct = default)
        //    => GetAndUnwrapAsync<List<FileInfoDto>>(
        //        $"{Base}/files-info?companyId={companyId}&year={year}&month={month:00}", ct);

        public Task<List<FileInfoDto>?> ListAsync(string companyId, int year, int month, CancellationToken ct = default)
        {
            string url = $"{Base}/files-info?companyId={companyId}&year={year}&month={month}";
            var list = GetAndUnwrapAsync<List<FileInfoDto>>(url, ct); 
            return list; ;

        }

        public Task<List<FileInfoDto>?> ListAsyncForDeclType(string companyId, int year, int month, string declType, CancellationToken ct = default)
        {
            var url = $"{Base}/files-info?companyId={companyId}&year={year}&month={month}";
            if (!string.IsNullOrWhiteSpace(declType))
                url += $"&declType={Uri.EscapeDataString(declType)}";

            return GetAndUnwrapAsync<List<FileInfoDto>>(url, ct);
        }
        public Task<FileDto?> GetDownloadAsync(int id, CancellationToken ct = default)
            => GetAndUnwrapAsync<FileDto>($"{Base}/download?id={id}", ct);

        // -------- Helpers --------
        private async Task<T?> GetAndUnwrapAsync<T>(string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!res.IsSuccessStatusCode) return default;

            var json = await res.Content.ReadAsStringAsync(ct);
            var env = JsonConvert.DeserializeObject<HttpDataResponse<T>>(json);
            return env.Data;
        }
    }
}
