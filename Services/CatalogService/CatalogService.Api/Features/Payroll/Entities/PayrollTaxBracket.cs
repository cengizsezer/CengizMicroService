namespace CatalogService.Api.Features.Payroll.Entities
{
    public class PayrollTaxBracket
    {
        public int Id { get; set; }

        public int Year { get; set; }

        public int Order { get; set; }

        public decimal MinAmount { get; set; }

        public decimal? MaxAmount { get; set; }

        /// <summary>
        /// Örn: %15 için 0.15m
        /// </summary>
        public decimal TaxRate { get; set; }
    }
}
