using System.Text.Json.Serialization;

namespace WebApp.Domain.Models.Catalog
{
    public class Expense
    {
        public int Id { get; set; }

        [JsonPropertyName("company")]
        public string Company { get; set; } = string.Empty;

        [JsonPropertyName("accountingCode")]
        public string AccountingCode { get; set; } = string.Empty;

        [JsonPropertyName("personnelCode")]
        public string PersonnelCode { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;

        [JsonPropertyName("amountExclVat")]
        public decimal AmountExclVat { get; set; }

        [JsonPropertyName("vatRate")]
        public decimal VatRate { get; set; }

        [JsonPropertyName("receiptDetails")]
        public List<ReceiptItem> ReceiptDetails { get; set; } = new();
    }
}

