namespace CatalogService.Api.Features.Payroll.Entities
{
    public class SeedHistory
    {
        public int Id { get; set; }
        public string SeedKey { get; set; } = default!;
        public int Version { get; set; }
        public DateTime AppliedAtUtc { get; set; }
    }
}
