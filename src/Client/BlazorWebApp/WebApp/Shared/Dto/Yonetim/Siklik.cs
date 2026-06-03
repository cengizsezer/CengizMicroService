using System.Text.Json.Serialization;

namespace WebApp.Shared.Dto.Yonetim
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Siklik
    {
        Gunluk = 0,
        Haftalik = 1
    }
}
