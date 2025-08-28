namespace IdentityService.Application.Models
{
    public class SelectTenantRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string TenantNo { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
