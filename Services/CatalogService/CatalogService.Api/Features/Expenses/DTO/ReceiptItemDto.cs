namespace CatalogService.Api.Features.Expenses.DTO
{
    public class ReceiptItemDto
    {
        public int Id { get; set; }

        /// <summary>
        /// Links this receipt item to a specific expense.
        /// </summary>
        public string ExpenseCode { get; set; } = string.Empty;

        /// <summary>
        /// Type of the expense item. Default: "Service".
        /// </summary>
        public string Type { get; set; } = "Hizmet";

        /// <summary>
        /// The accounting code assigned to this item.
        /// </summary>
        public string AccountingCode { get; set; } = string.Empty;

        /// <summary>
        /// Description tied to the accounting code (auto-filled).
        /// </summary>
        public string AccountingCodeDescription { get; set; } = string.Empty;

        /// <summary>
        /// Custom description entered by the user.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Quantity of the item. Default is 1.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Unit of measurement. Default is "Piece".
        /// </summary>
        public string Unit { get; set; } = "Adet";

        /// <summary>
        /// Total amount including VAT.
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Total VAT amount.
        /// </summary>
        public decimal TotalVat { get; set; }

        /// <summary>
        /// Receipt number.
        /// </summary>
        public string ReceiptNumber { get; set; } = string.Empty;

        /// <summary>
        /// Date of the receipt.
        /// </summary>
        public DateTime ReceiptDate { get; set; }

        /// <summary>
        /// Foreign key to the parent expense.
        /// </summary>
        public int ExpenseId { get; set; }

        /// <summary>
        /// Navigation property for the parent expense.
        /// </summary>
        public ExpenseDto Expense { get; set; } = new();

        /// <summary>
        /// List of product-level details under this receipt.
        /// </summary>
        public List<ProductDetailDto> ProductDetails { get; set; } = new();
    }
}
