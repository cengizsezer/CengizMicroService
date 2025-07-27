using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OCRService.Api.Services;

public class OpenAiInterpreter
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<OpenAiInterpreter> _logger;

    public OpenAiInterpreter(IHttpClientFactory factory, IConfiguration config, ILogger<OpenAiInterpreter> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    public async Task<string> InterpretAsync(string inputText)
    {
        var client = _factory.CreateClient();
        var apiKey = _config["OpenAI:ApiKey"];
        var model = _config["OpenAI:Model"] ?? "gpt-4o";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("OpenAI API key bulunamadı. 'OpenAI:ApiKey' config'e eklenmeli.");
            return "API anahtarı eksik.";
        }

        var prompt = """
        Aşağıdaki OCR çıktısını analiz et ve sadece şu formatta JSON döndür:

        {
          "firmaAdi": "tam firma adı",
          "belgeNumarasi": "fiş/fatura no",
          "netTutar": decimal, // KDV hariç toplam
          "kdvDetaylari": {
            "%1":   { "net": decimal, "kdv": decimal },
            "%10":  { "net": decimal, "kdv": decimal },
            "%20":  { "net": decimal, "kdv": decimal }
          }
        }

        Sadece OCR içeriğinden çıkarılabilen oranları ekle. Virgül yerine nokta kullan. Açıklama yazma, sadece geçerli bir JSON üret.
        """;

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = inputText }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var response = await client.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("OpenAI Raw Response: {0}", responseJson);

            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentElement))
            {
                var result = contentElement.GetString()?.Trim();

                // Basit JSON doğrulama
                if (!string.IsNullOrWhiteSpace(result) &&
                    result.StartsWith("{") &&
                    result.EndsWith("}"))
                {
                    return result;
                }

                return $"⚠ Beklenen JSON formatı alınamadı:\n{result}";
            }

            _logger.LogError("⚠ OpenAI response formatı beklenmiyor: {0}", responseJson);
            return "OpenAI cevabı beklenen formatta değil.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ OpenAI cevabı işlenirken hata oluştu.");
            return $"OpenAI işlem hatası: {ex.Message}";
        }
    }
}
