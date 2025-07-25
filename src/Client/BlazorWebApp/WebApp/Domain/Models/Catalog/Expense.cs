using Newtonsoft.Json;

namespace WebApp.Domain.Models.Catalog
{
    public class Expense
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("company")]
        public string Company { get; set; } = string.Empty;

        [JsonProperty("accountingCode")]
        public string AccountingCode { get; set; } = string.Empty;

        [JsonProperty("personnelCode")]
        public string PersonnelCode { get; set; } = string.Empty;

        [JsonProperty("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonProperty("note")]
        public string Note { get; set; } = string.Empty;

        [JsonProperty("amountExclVat")]
        public decimal AmountExclVat { get; set; }

        [JsonProperty("vatRate")]
        public decimal VatRate { get; set; }

        [JsonProperty("receiptDetails")]
        public List<ReceiptItem> ReceiptDetails { get; set; } = new();
    }
}
