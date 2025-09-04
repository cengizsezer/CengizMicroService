using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using WebApp.Application.Services.Interfaces;
using WebApp.Extensions;
using WebApp.Shared.Dto.CompanyDoc;
using WebApp.Shared.Dto.Declaration;

namespace WebApp.Application.Services
{
    public class FileApiService : IFileApiService
    {
        private readonly HttpClient _http;

        public FileApiService(HttpClient http) => _http = http;
        private static readonly Dictionary<string, string> MimeByExt = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
        public async Task<bool> UploadAsync(
            IBrowserFile file, string companyId, int year, int month,
            string declType, string docType, CancellationToken ct = default)
        {
            using var content = new MultipartFormDataContent();

            // Dosya
            var stream = file.OpenReadStream(20 * 1024 * 1024, ct); // 20MB

            // content-type fallback: eğer boş ya da octet-stream ise uzantıdan tahmin et
            var contentType = file.ContentType;
            if (string.IsNullOrWhiteSpace(contentType) || contentType == "application/octet-stream")
            {
                var ext = Path.GetExtension(file.Name);
                if (!string.IsNullOrEmpty(ext) && MimeByExt.TryGetValue(ext, out var mime))
                    contentType = mime;
            }

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(contentType ?? "application/octet-stream");

            content.Add(fileContent, "file", file.Name);

            // Form alanları
            content.Add(new StringContent(companyId), "companyId");
            content.Add(new StringContent(year.ToString()), "year");
            content.Add(new StringContent(month.ToString()), "month");
            content.Add(new StringContent(declType), "declType");
            content.Add(new StringContent(docType), "docType");

            using var resp = await _http.PostAsync("upload", content, ct);
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
            string url = $"files-info?companyId={companyId}&year={year}&month={month}";
            var list = GetAndUnwrapAsync<List<FileInfoDto>>(url, ct); 
            return list; ;

        }

        public Task<List<FileInfoDto>?> ListAsyncForDeclType(string companyId, int year, int month, string declType, CancellationToken ct = default)
        {
            var url = $"files-info?companyId={companyId}&year={year}&month={month}";
            if (!string.IsNullOrWhiteSpace(declType))
                url += $"&declType={Uri.EscapeDataString(declType)}";

            return GetAndUnwrapAsync<List<FileInfoDto>>(url, ct);
        }
        public Task<FileDto?> GetDownloadAsync(int id, CancellationToken ct = default)
            => GetAndUnwrapAsync<FileDto>($"download?id={id}", ct);

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


        public async Task<bool> UploadCompanyDocAsync(
    IBrowserFile file, string companyId, int year,
    string docCategory, string? description = null, int? sequenceNo = null,
    CancellationToken ct = default)
        {
            using var content = new MultipartFormDataContent();

            var stream = file.OpenReadStream(20 * 1024 * 1024, ct);
            var fileContent = new StreamContent(stream);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType;
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", file.Name);

            content.Add(new StringContent(companyId), "companyId");
            content.Add(new StringContent(docCategory), "docCategory");
            content.Add(new StringContent(year.ToString()), "year");
            if (!string.IsNullOrWhiteSpace(description)) content.Add(new StringContent(description), "description");
            if (sequenceNo.HasValue) content.Add(new StringContent(sequenceNo.Value.ToString()), "sequenceNo");

            using var resp = await _http.PostAsync("company-docs/upload", content, ct);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync(ct);
            var env = JsonConvert.DeserializeObject<HttpDataResponse<bool>>(json);
            return env?.Data == true;
        }

        public Task<List<CompanyDocInfoDto>?> ListCompanyDocsAsync(string companyId, string? year = null, string? docCategory = null, CancellationToken ct = default)
        {
            var url = $"company-docs/list?companyId={Uri.EscapeDataString(companyId)}";
            if (!string.IsNullOrWhiteSpace(year)) url += $"&year={Uri.EscapeDataString(year)}";
            if (!string.IsNullOrWhiteSpace(docCategory)) url += $"&docCategory={Uri.EscapeDataString(docCategory)}";
            return GetAndUnwrapAsync<List<CompanyDocInfoDto>>(url, ct);
        }
    }
}
