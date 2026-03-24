namespace CatalogService.Api.Features.Declarations.Queries
{
    public class MonthlyDeclarationSummaryQuery
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public string? TenantNo { get; set; }
        public string? DeclarationType { get; set; }
        public int? CustomerCompanyId { get; set; }
    }
}
