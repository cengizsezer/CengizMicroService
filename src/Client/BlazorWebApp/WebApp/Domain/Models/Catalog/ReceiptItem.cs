using Newtonsoft.Json;

namespace WebApp.Domain.Models.Catalog
{
    public class ReceiptItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("item")]
        public string Item { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("vatRate")]
        public decimal VatRate { get; set; }

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

        [JsonProperty("expenseId")]
        public int ExpenseId { get; set; }

        [JsonProperty("expense")]
        public Expense? Expense { get; set; }

        [JsonProperty("productDetails")]
        public List<ProductDetail> ProductDetails { get; set; } = new();
    }
}
