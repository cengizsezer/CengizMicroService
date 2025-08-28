namespace IdentityService.Domain.Entities
{
    public class UserTenantRole
    {
        public int UserId { get; set; }
        public int TenantId { get; set; }
        public int RoleId { get; set; }

        // İSTEĞİMİZ: SADECE BUNLAR
        public Role Role { get; set; } = default!;

        // İSTEMİYORUZ (kaldırın):
        // public User User { get; set; } = default!;
        // public Tenant Tenant { get; set; } = default!;
        // (İstersen UserTenant nav’ı ekleyebilirsin ama şart değil)
        // public UserTenant UserTenant { get; set; } = default!;
    }
}
