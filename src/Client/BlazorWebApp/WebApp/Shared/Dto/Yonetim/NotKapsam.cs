using System.Text.Json.Serialization;

namespace WebApp.Shared.Dto.Yonetim
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NotKapsam
    {
        Gun = 0,
        Ay = 1,
        Genel = 2
    }
}
