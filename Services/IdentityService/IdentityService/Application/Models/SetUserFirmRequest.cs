namespace IdentityService.Application.Models
{
    public class SetUserFirmsRequest
    {
        public List<UserFirmAssignDto> Firms { get; set; } = new();
    }

    public class UserFirmAssignDto
    {
        public int TenantId { get; set; }
        public List<string>? Roles { get; set; }  // null ise o tenant’ın rolleri değiştirilmesin istersen “merge” mantığı yazabilirsin
    }


    public class UserFirmDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = "";
        public List<string> Roles { get; set; } = new();
    }
}
