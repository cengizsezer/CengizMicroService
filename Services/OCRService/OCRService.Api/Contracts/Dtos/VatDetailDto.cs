using System.Text.Json.Serialization;

namespace OCRService.Api.Contracts.Dtos
{
    public sealed class VatDetailDto
    {
       
        public decimal BaseAmount { get; set; }

        public decimal BaseVat { get; set; }
    }
}
