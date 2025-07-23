namespace IdentityService.Application.Models
{
    public class RefreshTokenRequestModel
    {
        public string UserName { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

}
