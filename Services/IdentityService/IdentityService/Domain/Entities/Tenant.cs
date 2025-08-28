namespace IdentityService.Domain.Entities
{
    public class Tenant
    {
        public int Id { get; set; }
        public string FirmaNo { get; set; } = default!;
        public string Ad { get; set; } = default!;
        public string? Vkn { get; set; }

        public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    }
}
