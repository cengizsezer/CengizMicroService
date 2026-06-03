using System.Text.Json.Serialization;

namespace CatalogService.Api.Features.Banka.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NotKapsam
    {
        // Belirli bir güne ait not (Tarih dolu).
        Gun = 0,
        // Belirli bir aya ait not (Yil + Ay dolu).
        Ay = 1,
        // Hesaba genel / sürekli not.
        Genel = 2
    }
}
