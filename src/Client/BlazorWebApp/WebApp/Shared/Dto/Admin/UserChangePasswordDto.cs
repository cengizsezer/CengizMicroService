using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Admin
{
    public sealed class UserChangePasswordDto
    {
        [Required, MinLength(6)]
        public string NewPassword { get; set; } = "";
    }
}
