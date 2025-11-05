using System.ComponentModel.DataAnnotations;

namespace IdentityService.Application.Models.Admin
{
    public sealed class UserChangePasswordDto
    {
        [Required, MinLength(6)]
        public string NewPassword { get; set; } = "";
    }
}
