using System.Text.Json.Serialization;

namespace WebApp.Shared.Dto.Yonetim
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MukellefDurumu
    {
        DevamEdiyor = 0,
        FeshOldu = 1,
        IptalEdildi = 2
    }
}
