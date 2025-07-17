namespace WebApp.Domain.Models.Catalog
{
    public class ReceiptItem
    {
        public int Id { get; set; }
        public string Item { get; set; } // Eski CatalogItem.Name
        public decimal Amount { get; set; } // Eski CatalogItem.Price
        public decimal VatRate { get; set; }

        public string AccountingCode { get; set; }
        public string PersonnelCode { get; set; }
        public string FullName { get; set; }
        public string Company { get; set; }
        public string Note { get; set; }
        public decimal AmountExclVat { get; set; }
        public int ExpenseId { get; set; }
        public Expense Expense { get; set; }

        public List<ProductDetail> ProductDetails { get; set; } = new();
    }
}
