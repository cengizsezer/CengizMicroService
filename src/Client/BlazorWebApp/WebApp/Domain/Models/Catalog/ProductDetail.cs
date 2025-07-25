using Newtonsoft.Json;

namespace WebApp.Domain.Models.Catalog
{
    public class ProductDetail
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("vatRate")]
        public decimal VatRate { get; set; }

        [JsonProperty("receiptItemId")]
        public int ReceiptItemId { get; set; }

        [JsonProperty("accountingCode")]
        public string AccountingCode { get; set; } = string.Empty;

        [JsonProperty("personnelCode")]
        public string PersonnelCode { get; set; } = string.Empty;

        [JsonProperty("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonProperty("company")]
        public string Company { get; set; } = string.Empty;

        [JsonProperty("note")]
        public string Note { get; set; } = string.Empty;

        [JsonProperty("amountExclVat")]
        public decimal AmountExclVat { get; set; }

        [JsonProperty("receiptItem")]
        public ReceiptItem? ReceiptItem { get; set; }
    }
}
