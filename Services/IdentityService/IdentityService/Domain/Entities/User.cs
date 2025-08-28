#nullable enable
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Domain.Entities
{
    public class User: IdentityUser<int>
    {
        // Eski Role alanı artık kullanılmayacak (rol atamaları tenant bazlı UserTenantRole üzerinden yapılacak)
        // İstersen tamamen silebilirsin, istersen "legacy" diye bırakabilirsin.
        public string? LegacyRole { get; set; }

        // RefreshToken artık ayrı tabloda (RefreshToken.cs) tutuluyor.
        // O yüzden bu property’lere gerek yok. Eğer bırakmak istersen debug amaçlı bırakabilirsin.
        // public string RefreshToken { get; set; } = string.Empty;
        // public DateTime RefreshTokenExpiryTime { get; set; }

        // Navigation: bu user hangi tenantlara bağlı?
        public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
    }
}
