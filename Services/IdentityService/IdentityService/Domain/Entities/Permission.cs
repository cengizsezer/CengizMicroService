namespace IdentityService.Domain.Entities
{
    public class Permission
    {
        public int Id { get; set; }
        public string Key { get; set; } = default!; // "Expense.View", "Expense.Edit"
        public string? Description { get; set; }

        public ICollection<RolePermission> Roles { get; set; } = new List<RolePermission>();
    }
}
