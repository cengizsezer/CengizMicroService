using System.Text.Json.Serialization;

namespace CatalogService.Api.Features.Mukellefler.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MukellefDurumu
    {
        DevamEdiyor = 0,
        FeshOldu = 1,
        IptalEdildi = 2
    }
}
