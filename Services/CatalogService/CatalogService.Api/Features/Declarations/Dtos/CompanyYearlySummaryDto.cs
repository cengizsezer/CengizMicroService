namespace CatalogService.Api.Features.Declarations.Dtos
{
    public class CompanyYearlySummaryDto
    {
        public string TenantNo { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public int Year { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PendingAmount { get; set; }

        public int DeclarationCount { get; set; }
        public int CustomerCompanyId { get; set; }
    }
}
