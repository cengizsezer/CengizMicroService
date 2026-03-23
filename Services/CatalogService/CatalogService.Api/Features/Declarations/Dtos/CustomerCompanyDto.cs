namespace CatalogService.Api.Features.Declarations.Dtos
{
    public class CustomerCompanyDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
    }
}
