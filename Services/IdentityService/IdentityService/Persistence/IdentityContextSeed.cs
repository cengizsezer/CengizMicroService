using IdentityService.Domain.Entities;

namespace IdentityService.Persistence
{
    public class IdentityContextSeed
    {
        public async Task SeedAsync(IdentityDbContext context, IWebHostEnvironment env, ILogger logger, bool force = false)
        {
            logger.LogInformation("🔍 SeedAsync başladı...");

            if (force)
            {
                context.Users.RemoveRange(context.Users);
                await context.SaveChangesAsync();
                logger.LogInformation("🧹 Mevcut kullanıcılar silindi.");
            }

            if (!context.Users.Any())
            {
                context.Users.Add(new User
                {
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"), // 🧠 Mutlaka hashle
                    RefreshToken = Guid.NewGuid().ToString(),
                    RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7),
                    Email = "admin@system.local",
                    Role = "Admin"
                });

                await context.SaveChangesAsync();
                logger.LogInformation("✅ Varsayılan admin kullanıcısı oluşturuldu.");
            }
            else
            {
                logger.LogInformation("ℹ️ Kullanıcılar zaten var, seed yapılmadı.");
            }
        }

    }
}
