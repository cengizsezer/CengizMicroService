#nullable enable
namespace CatalogService.Api.Core.Domain
{
    public class Expense
    {
        public int Id { get; set; }

        public string Company { get; set; } = string.Empty;

        public string AccountingCode { get; set; } = string.Empty;

        public string PersonnelCode { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        public decimal AmountExclVat { get; set; }

        public decimal VatRate { get; set; }

        public List<ReceiptItem> ReceiptDetails { get; set; } = new();
    }
}
