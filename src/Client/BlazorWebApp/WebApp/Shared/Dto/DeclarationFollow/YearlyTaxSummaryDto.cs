namespace CatalogService.Api.Features.Declarations.Dtos
{
    public class YearlyTaxSummaryDto
    {
        public int Year { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public int TotalCompanyCount { get; set; }
        public int TotalDeclarationCount { get; set; }
        public int CustomerCompanyId { get; set; }
    }
}
