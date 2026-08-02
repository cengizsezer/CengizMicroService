using CatalogService.Api.Infrastructure.Auth;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>Testler için sabit kimlikli kullanıcı; gerçekte claim'ler token'dan gelir.</summary>
    public sealed class SabitKullanici : IHttpCurrentUser
    {
        public SabitKullanici(int kullaniciId = 7) => UserId = kullaniciId.ToString();

        public bool IsAuthenticated => true;
        public string UserId { get; }
        public string? UserName => "test";
        public string? Email => "test@example.com";
    }
}
