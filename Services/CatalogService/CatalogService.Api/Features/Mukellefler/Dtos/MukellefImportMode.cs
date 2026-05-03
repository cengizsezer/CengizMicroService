using System.Text.Json.Serialization;

namespace CatalogService.Api.Features.Mukellefler.Dtos
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MukellefImportMode
    {
        UpdateExisting = 0,
        SkipExisting = 1
    }
}
