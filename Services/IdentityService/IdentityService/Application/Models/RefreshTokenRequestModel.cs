namespace IdentityService.Application.Models
{
    public class RefreshTokenRequestModel
    {
        public string UserName { get; set; }
        public string RefreshToken { get; set; }
    }

}
