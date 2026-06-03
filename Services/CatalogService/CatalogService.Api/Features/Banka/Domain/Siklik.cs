using System.Text.Json.Serialization;

namespace CatalogService.Api.Features.Banka.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Siklik
    {
        Gunluk = 0,
        Haftalik = 1
    }
}
