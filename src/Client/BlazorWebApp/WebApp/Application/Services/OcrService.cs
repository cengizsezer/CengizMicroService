using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto;

namespace WebApp.Application.Services
{
    public class OcrService:IOcrService
    {
        private readonly HttpClient _httpClient;

        public OcrService(HttpClient http)
        {
            _httpClient = http;
           
        }

        public async Task<AnalyzeResponseDto?> AnalyzeAsync(IBrowserFile file, CancellationToken ct = default)
        {
            using var content = new MultipartFormDataContent();

            // 50 MB sınır, ihtiyacına göre düşür/ artır
            using var stream = file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "image/png");

            // !!! Backend'de param adı IFormFile file => "file" olmalı
            content.Add(fileContent, "file", file.Name);

            // (Opsiyonel) Auth header
            // _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _httpClient.PostAsync("ocr/analyze", content, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"OCR analyze failed: {(int)resp.StatusCode} {resp.ReasonPhrase} -> {body}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonConvert.DeserializeObject<AnalyzeResponseDto>(json);
        }
    }
}
