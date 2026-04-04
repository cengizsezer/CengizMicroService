namespace CatalogService.Api.Features.Payment.Entities
{
    public class TaxPaymentEntity
    {
        public int Id { get; set; }

        public string TahakkukNo { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        public string TaxpayerName { get; set; } = string.Empty;   // mükellef
        public string TaxType { get; set; } = string.Empty;        // vergi türü
        public string CreatedBy { get; set; } = string.Empty;      // giren kişi
        public string Description { get; set; } = string.Empty;    // açıklama
    }
}
