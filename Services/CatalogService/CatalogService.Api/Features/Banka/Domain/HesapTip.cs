using System.Text.Json.Serialization;

namespace CatalogService.Api.Features.Banka.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HesapTip
    {
        Banka = 0,
        KrediKarti = 1
    }
}
