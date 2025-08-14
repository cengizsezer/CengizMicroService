#nullable enable
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Domain.Entities
{
    public class User: IdentityUser<int>
    {
        public string Role { get; set; } = string.Empty;
        public string RefreshToken { get; set; }=string.Empty;
        public DateTime RefreshTokenExpiryTime { get; set; }

        public ICollection<UserFirm> UserFirmalar { get; set; } = new List<UserFirm>();
    }
}
