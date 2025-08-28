namespace IdentityService.Domain.Entities
{
    public class UserTenant
    {
        public int UserId { get; set; }
        public User User { get; set; } = default!;
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;

        public ICollection<UserTenantRole> Roles { get; set; } = new List<UserTenantRole>();
    }
}
