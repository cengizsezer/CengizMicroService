using System.Text.Json.Serialization;

namespace WebApp.Domain.Models.Catalog
{
    public class ProductDetail
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("vatRate")]
        public decimal VatRate { get; set; }

        [JsonPropertyName("receiptItemId")]
        public int ReceiptItemId { get; set; }

        [JsonPropertyName("accountingCode")]
        public string AccountingCode { get; set; } = string.Empty;

        [JsonPropertyName("personnelCode")]
        public string PersonnelCode { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("company")]
        public string Company { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;

        [JsonPropertyName("amountExclVat")]
        public decimal AmountExclVat { get; set; }

        [JsonPropertyName("receiptItem")]
        public ReceiptItem? ReceiptItem { get; set; }
    }
}
