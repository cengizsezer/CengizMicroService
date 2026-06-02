using System.Text.Json.Serialization;

namespace WebApp.Shared.Dto.Yonetim
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FinansSinifi
    {
        A = 0,
        B = 1,
        C = 2
    }
}
