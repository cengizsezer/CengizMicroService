using System.Text.Json.Serialization;

namespace WebApp.Shared.Dto.Yonetim
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HesapTip
    {
        Banka = 0,
        KrediKarti = 1
    }
}
