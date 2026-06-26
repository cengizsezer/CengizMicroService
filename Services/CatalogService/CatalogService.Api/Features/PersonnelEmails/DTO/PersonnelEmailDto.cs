namespace CatalogService.Api.Features.PersonnelEmails.DTO
{
    public class PersonnelEmailDto
    {
        public string UserId { get; set; } = "";
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
