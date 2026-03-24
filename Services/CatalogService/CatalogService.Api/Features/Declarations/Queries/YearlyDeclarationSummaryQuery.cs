namespace CatalogService.Api.Features.Declarations.Queries
{
    public class YearlyDeclarationSummaryQuery
    {
        public int Year { get; set; }
        public string? TenantNo { get; set; }
        public int? CustomerCompanyId { get; set; }
    }
}
