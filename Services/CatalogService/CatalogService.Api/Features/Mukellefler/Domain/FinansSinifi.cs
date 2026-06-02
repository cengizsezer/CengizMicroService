using System.Text.Json.Serialization;

namespace CatalogService.Api.Features.Mukellefler.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FinansSinifi
    {
        A = 0,
        B = 1,
        C = 2
    }
}
