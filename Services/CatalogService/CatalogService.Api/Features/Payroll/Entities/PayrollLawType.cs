namespace CatalogService.Api.Features.Payroll.Entities
{
    public class PayrollLawType
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int Year { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
    }
}
