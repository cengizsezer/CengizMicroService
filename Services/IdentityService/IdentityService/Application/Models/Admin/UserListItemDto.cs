namespace IdentityService.Application.Models.Admin
{
    public class UserListItemDto
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? FirmName { get; set; }  // tek aktif firma gösterimi için opsiyonel
        public string[] Roles { get; set; } = Array.Empty<string>();
    }
}
