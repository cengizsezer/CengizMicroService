
namespace OCRService.Api.Contracts.Dtos
{
    public class OcrInterpretationDto
    {
       
        public string CompanyName { get; set; } = string.Empty;

        public string InvoiceNumber { get; set; } = string.Empty;

        public decimal BaseAmount { get; set; }
       
        public Dictionary<string, VatDetailDto> LsVatDetails { get; set; } =new Dictionary<string, VatDetailDto>();
    }
}
