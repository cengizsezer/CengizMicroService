namespace CatalogService.Api.Features.Payment.DTO
{
    public class TaxPaymentEntityDto
    {
        public int Id { get; set; }

        public string TahakkukNo { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        public string TaxpayerName { get; set; } = string.Empty;
        public string TaxType { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
