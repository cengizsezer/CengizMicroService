using System.Text.Json;

namespace WebApp.Application.Services.Yonetim
{
    internal static class ApiErrorParser
    {
        public static async Task ThrowAsync(HttpResponseMessage response, CancellationToken ct)
        {
            string? field = null;
            string message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

            try
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("field", out var fieldElem) && fieldElem.ValueKind == JsonValueKind.String)
                            field = fieldElem.GetString();

                        if (root.TryGetProperty("message", out var msgElem) && msgElem.ValueKind == JsonValueKind.String)
                            message = msgElem.GetString() ?? message;
                        else if (root.TryGetProperty("title", out var titleElem) && titleElem.ValueKind == JsonValueKind.String)
                            message = titleElem.GetString() ?? message;
                    }
                }
            }
            catch
            {
                // body parse error, fall back to default message
            }

            throw new ApiClientException(response.StatusCode, message, field);
        }
    }
}
