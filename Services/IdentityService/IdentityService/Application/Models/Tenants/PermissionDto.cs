namespace IdentityService.Application.Models.Tenants
{
    public class PermissionDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = default!;
        public string? Description { get; set; }
    }
}
