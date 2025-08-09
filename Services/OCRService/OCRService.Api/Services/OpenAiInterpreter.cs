using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCRService.Api.Contracts.Dtos;

namespace OCRService.Api.Services
{
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

        /// <summary>
        /// OCR metnini yorumlar ve OcrInterpretationDto döner.
        /// </summary>
        public async Task<OcrInterpretationDto?> InterpretAsync(string inputText, CancellationToken ct = default)
        {
            var client = _factory.CreateClient();
            var apiKey = _config["OpenAI:ApiKey"];
            var model = _config["OpenAI:Model"] ?? "gpt-4o";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("OpenAI API key bulunamadı. 'OpenAI:ApiKey' config'e eklenmeli.");
                return null;
            }

            // FIELD İSİMLERİ SENİN DTO'LARLA BİREBİR!
            var systemPrompt = """
            Aşağıdaki OCR çıktısını analiz et ve SADECE şu yapıda GEÇERLİ JSON döndür:
            {
              "CompanyName": "tam firma adı",
              "InvoiceNumber": "fiş/fatura no",
              "BaseAmount": number,
              "LsVatDetails": {
                "%1":  { "BaseAmount": number, "BaseVat": number },
                "%10": { "BaseAmount": number, "BaseVat": number },
                "%20": { "BaseAmount": number, "BaseVat": number }
              }
            }
            Kurallar:
            - Sadece bulabildiğin oranları koy.
            - Sayılarda nokta kullan (örn: 976.43).
            - Ekstra açıklama yok, sadece JSON.
            """;

            var body = new
            {
                model,
                temperature = 0,
                max_tokens = 600,
                response_format = new { type = "json_object" }, // JSON zorlaması
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = inputText }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                using var resp = await client.SendAsync(req, ct);
                var respText = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("OpenAI Raw Response: {resp}", respText);

                var root = JObject.Parse(respText);
                var content = root["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

                // code-fence temizle
                content = content.Replace("```json", "").Replace("```", "").Trim();

                if (string.IsNullOrWhiteSpace(content) || !content.StartsWith("{") || !content.EndsWith("}"))
                {
                    _logger.LogWarning("Beklenen JSON content bulunamadı. Content: {content}", content);
                    return null;
                }

                // JSON'u DTO'ya çevir
                var dto = JsonConvert.DeserializeObject<OcrInterpretationDto>(content);
                if (dto == null)
                    return null;

                // KDV oranlarından eksik BaseAmount'ları tamamla (BaseAmount = BaseVat * 100 / yüzde)
                if (dto.LsVatDetails != null)
                {
                    foreach (var kv in dto.LsVatDetails.ToList())
                    {
                        var key = (kv.Key ?? "").Trim().Replace("%", "");
                        if (decimal.TryParse(key, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct) && pct > 0)
                        {
                            var v = kv.Value ?? new VatDetailDto();
                            if (v.BaseAmount == 0 && v.BaseVat > 0)
                            {
                                // Örn: %20 ve KDV=0.97 ise matrah = 0.97 * 100 / 20 = 4.85
                                v.BaseAmount = Math.Round(v.BaseVat * 100m / pct, 2, MidpointRounding.AwayFromZero);
                                dto.LsVatDetails[kv.Key] = v;
                            }
                        }
                    }
                }

                // Üstteki BaseAmount (net toplam) yoksa/0 ise detaylardan topla
                if (dto.BaseAmount <= 0 && dto.LsVatDetails != null)
                {
                    var sumNet = dto.LsVatDetails.Values.Sum(x => x?.BaseAmount ?? 0m);
                    if (sumNet > 0) dto.BaseAmount = sumNet;
                }

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI interpret hatası");
                return null;
            }
        }
    }
}
