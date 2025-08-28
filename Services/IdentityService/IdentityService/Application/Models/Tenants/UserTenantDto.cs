namespace IdentityService.Application.Models.Tenants
{
    public class UserTenantDto
    {
        public int UserId { get; set; }
        public string FirmaNo { get; set; } = default!;
        public List<string> Roles { get; set; } = new();
    }
}
