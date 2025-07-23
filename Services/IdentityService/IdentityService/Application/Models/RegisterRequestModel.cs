namespace IdentityService.Application.Models
{
    public class RegisterRequestModel
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }  // opsiyonel
    }
}
