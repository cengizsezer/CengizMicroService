namespace CatalogService.Api.Features.Payroll.Entities
{
    public class PayrollLawIncentiveRule
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public int Year { get; set; }
        public decimal SgkEmployerIncentiveRate { get; set; }
        public bool IsImplemented { get; set; }
    }
}
