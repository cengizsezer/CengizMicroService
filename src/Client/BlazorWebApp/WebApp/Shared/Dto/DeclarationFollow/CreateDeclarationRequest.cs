using CatalogService.Api.Features.Declarations.Entities;

namespace CatalogService.Api.Features.Declarations.Dtos
{
    public class CreateDeclarationRequest
    {
        public string TenantNo { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        public string DeclarationType { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }

        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }

        public DeclarationStatus DeclarationStatus { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public DateTime? PaymentDate { get; set; }
        public string? Note { get; set; }
        public int CustomerCompanyId { get; set; }
    }
}
