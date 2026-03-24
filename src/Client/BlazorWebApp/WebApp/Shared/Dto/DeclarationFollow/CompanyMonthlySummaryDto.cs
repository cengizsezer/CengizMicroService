namespace CatalogService.Api.Features.Declarations.Dtos
{
    public class CompanyMonthlySummaryDto
    {
        public string TenantNo { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        public int Year { get; set; }
        public int Month { get; set; }

        public int DeclarationCount { get; set; }
        public int ApprovedCount { get; set; }
        public int PaidCount { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PendingAmount { get; set; }

        public List<DeclarationDto> Declarations { get; set; } = new();

        public int CustomerCompanyId { get; set; }
    }
}
