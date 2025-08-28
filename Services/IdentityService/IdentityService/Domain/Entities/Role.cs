namespace IdentityService.Domain.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!; // "MaliIsler"

        public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
        public ICollection<UserTenantRole> Users { get; set; } = new List<UserTenantRole>();
    }
}
