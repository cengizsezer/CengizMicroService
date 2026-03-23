namespace CatalogService.Api.Features.Declarations.Entities
{
    public class Declaration
    {
        public int Id { get; set; }

        public string TenantNo { get; set; } = default!;
        public string CompanyName { get; set; } = string.Empty;

        public string DeclarationType { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }

        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }

        public DeclarationStatus DeclarationStatus { get; set; } = DeclarationStatus.Draft;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public DateTime? PaymentDate { get; set; }
        public string? Note { get; set; }
    }
}
