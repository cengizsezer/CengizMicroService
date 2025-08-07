using Newtonsoft.Json;

namespace WebApp.Domain.Models.Catalog
{
    public class Expense
    {
        public int Id { get; set; }

        /// <summary>
        /// Unique code to identify the expense record.
        /// </summary>
        public string ExpenseCode { get; set; } = string.Empty;

        /// <summary>
        /// Full name of the employee who created the expense.
        /// </summary>
        public string PersonnelFullName { get; set; } = string.Empty;

        /// <summary>
        /// Accounting code of the personnel.
        /// </summary>
        public string PersonnelAccountingCode { get; set; } = string.Empty;

        /// <summary>
        /// The date when the expense occurred.
        /// </summary>
        public DateTime ExpenseDate { get; set; }

        /// <summary>
        /// Code of the related project.
        /// </summary>
        public string ProjectCode { get; set; } = string.Empty;

        /// <summary>
        /// Date when the record was created.
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Time of creation (optional if separate from CreatedDate).
        /// </summary>
        public TimeSpan CreatedTime { get; set; }

        /// <summary>
        /// Full name or username of the user who approved the expense.
        /// </summary>
        public string ApprovedBy { get; set; } = string.Empty;

        /// <summary>
        /// DateTime when the expense was approved.
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// Total amount of the expense including VAT.
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Total VAT amount of the expense.
        /// </summary>
        public decimal TotalVat { get; set; }

        /// <summary>
        /// Additional description or notes about the expense.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Collection of associated receipt items (one-to-many).
        /// </summary>
        public List<ReceiptItem> ReceiptItems { get; set; } = new();
    }
}
