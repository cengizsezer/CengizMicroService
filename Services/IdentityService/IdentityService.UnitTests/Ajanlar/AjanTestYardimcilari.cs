using IdentityService.Application.Services.Agent;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityService.UnitTests.Ajanlar
{
    /// <summary>
    /// İleri sürülebilen saat. "Anahtarın süresi dolunca token verilmiyor" gibi
    /// kuralları gerçek zamanı bekleyerek sınamak mümkün değil.
    /// </summary>
    public class SahteSaat : TimeProvider
    {
        private DateTimeOffset _simdi;

        public SahteSaat(DateTimeOffset? baslangic = null)
            => _simdi = baslangic ?? new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _simdi;

        public void Ilerle(TimeSpan sure) => _simdi = _simdi.Add(sure);
    }

    /// <summary>
    /// Servisi belleğe kurulmuş bir veritabanıyla ayağa kaldırır. Sınanan şey
    /// SQL Server davranışı değil, anahtar/token kuralları.
    /// </summary>
    public static class AjanTestKurulumu
    {
        public const string ImzaAnahtari = "super_secret_dev_key_32bytes_minimum";
        public const string Issuer = "identityserver.tr";
        public const string Audience = "identityclient.tr";

        public static IdentityDbContext Db()
        {
            var secenekler = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase($"ajan-{Guid.NewGuid():N}")
                .Options;

            return new IdentityDbContext(secenekler);
        }

        public static IConfiguration Ayarlar() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = ImzaAnahtari,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience
                })
                .Build();

        public static AjanKimlikServisi Servis(IdentityDbContext db, TimeProvider saat) =>
            new(db,
                new PasswordHasher<Ajan>(),
                Ayarlar(),
                saat,
                NullLogger<AjanKimlikServisi>.Instance);
    }
}
