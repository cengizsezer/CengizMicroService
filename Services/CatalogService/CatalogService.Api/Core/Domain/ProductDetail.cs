namespace CatalogService.Api.Core.Domain
{
    public class ProductDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } // Eski CatalogType.Type olabilir veya yeni
        public decimal Amount { get; set; }
        public decimal VatRate { get; set; }

        public int ReceiptItemId { get; set; }

        public string AccountingCode { get; set; }
        public string PersonnelCode { get; set; }
        public string FullName { get; set; }
        public string Company { get; set; }
        public string Note { get; set; }
        public decimal AmountExclVat { get; set; }
        public ReceiptItem ReceiptItem { get; set; }
    }
}
