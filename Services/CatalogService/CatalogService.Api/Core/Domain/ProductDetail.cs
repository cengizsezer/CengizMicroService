#nullable enable
namespace CatalogService.Api.Core.Domain
{
    public class ProductDetail
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal VatRate { get; set; }

        public int ReceiptItemId { get; set; }

        public string AccountingCode { get; set; } = string.Empty;

        public string PersonnelCode { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Company { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        public decimal AmountExclVat { get; set; }

        public ReceiptItem ReceiptItem { get; set; } = new();
    }
}
