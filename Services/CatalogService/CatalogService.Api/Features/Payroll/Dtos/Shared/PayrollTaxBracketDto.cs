namespace CatalogService.Api.Features.Payroll.Dtos.Shared
{
    public class PayrollTaxBracketDto
    {
        public int Order { get; set; }
        public decimal MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public decimal TaxRate { get; set; }
    }
}
