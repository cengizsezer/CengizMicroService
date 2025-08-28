namespace IdentityService.Application.Models.Tenants
{
    public class UserTenantRoleDto
    {
        public int UserId { get; set; }
        public string FirmaNo { get; set; } = default!;
        public string RoleName { get; set; } = default!;
    }
}
