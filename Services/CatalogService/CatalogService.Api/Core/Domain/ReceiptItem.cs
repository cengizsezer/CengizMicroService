#nullable enable
namespace CatalogService.Api.Core.Domain
{
    public class ReceiptItem
    {
        public int Id { get; set; }

        public string Item { get; set; } = string.Empty; // Eski CatalogItem.Name

        public decimal Amount { get; set; }

        public decimal VatRate { get; set; }

        public string AccountingCode { get; set; } = string.Empty;

        public string PersonnelCode { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Company { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        public decimal AmountExclVat { get; set; }

        public int ExpenseId { get; set; }

        public Expense Expense { get; set; } = new();

        public List<ProductDetail> ProductDetails { get; set; } = new();
    }
}
