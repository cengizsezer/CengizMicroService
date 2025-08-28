namespace IdentityService.Application.Models.Auth
{
    public class MeResponseModel
    {
        public string Username { get; set; } = string.Empty;
        public string? TenantNo { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public List<FirmaDto> Firmalar { get; set; } = new();
    }

}
