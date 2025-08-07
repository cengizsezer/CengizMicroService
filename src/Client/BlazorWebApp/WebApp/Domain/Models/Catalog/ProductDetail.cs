using Newtonsoft.Json;

namespace WebApp.Domain.Models.Catalog
{
    public class ProductDetail
    {
        public int Id { get; set; }

        /// <summary>
        /// Display order of the product in the receipt item.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Tax base amount (excluding VAT).
        /// </summary>
        public decimal TaxBase { get; set; }

        /// <summary>
        /// VAT rate (e.g., 0.20 for 20%).
        /// </summary>
        public decimal VatRate { get; set; }

        /// <summary>
        /// VAT amount calculated from the tax base and rate.
        /// </summary>
        public decimal VatAmount { get; set; }

        /// <summary>
        /// Total amount (TaxBase + VatAmount).
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Foreign key to the parent receipt item.
        /// </summary>
        public int ReceiptItemId { get; set; }

        /// <summary>
        /// Navigation property to the parent receipt item.
        /// </summary>
        public ReceiptItem ReceiptItem { get; set; } = new();
    }
}
