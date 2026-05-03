using System.Text.Json.Serialization;

namespace WebApp.Shared.Dto.Yonetim
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MukellefImportMode
    {
        UpdateExisting = 0,
        SkipExisting = 1
    }
}
