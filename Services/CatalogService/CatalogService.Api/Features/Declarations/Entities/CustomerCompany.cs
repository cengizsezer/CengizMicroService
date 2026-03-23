using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.Declarations.Entities
{
    public class CustomerCompany : TenantEntity
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public string? TaxOffice { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Note { get; set; }
    }
}
