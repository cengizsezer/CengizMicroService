namespace CatalogService.Api.Infrastructure.DTO
{
    public class UserMiniDto
    {
        public string Id { get; set; } = default!;
        public string FullName { get; set; } = default!;
        public string? Email { get; set; }
    }
}
