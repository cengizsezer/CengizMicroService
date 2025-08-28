namespace IdentityService.Application.Models.Tenants
{
    public class TenantDto
    {
        public string FirmaNo { get; set; } = default!;
        public string Ad { get; set; } = default!;
        public string? Vkn { get; set; }
    }

}
