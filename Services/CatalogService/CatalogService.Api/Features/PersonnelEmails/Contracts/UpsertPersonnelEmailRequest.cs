namespace CatalogService.Api.Features.PersonnelEmails.Contracts
{
    public class UpsertPersonnelEmailRequest
    {
        public string UserId { get; set; } = "";
        public string? UserName { get; set; }
        public string? Email { get; set; }
    }
}
