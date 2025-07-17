namespace CatalogService.Api.Core.Domain
{
    public class Expense
    {
        public int Id { get; set; }
        public string Company { get; set; } // Eski CatalogBrand.Brand

        public string AccountingCode { get; set; }
        public string PersonnelCode { get; set; }
        public string FullName { get; set; }
        public string Note { get; set; }
        public decimal AmountExclVat { get; set; }
        public decimal VatRate { get; set; }
        public List<ReceiptItem> ReceiptDetails { get; set; } = new();
    }
}
