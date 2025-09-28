using Blazored.SessionStorage;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Infrastructure
{
    /// <summary>
    /// Koridordan geçen her isteğe (anonim olanlar hariç) Bearer token ekler.
    /// </summary>
    public sealed class AuthTokenHandler : DelegatingHandler
    {
        private readonly ISessionStorageService _session;

        public AuthTokenHandler(ISessionStorageService session) => _session = session;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            // Anonim auth endpoint’leri: token ekleme
            var isPublicAuthEndpoint =
                path.Equals("/auth/login", System.StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/auth/register", System.StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/auth/select-tenant", System.StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/auth/refresh-token", System.StringComparison.OrdinalIgnoreCase);

            if (!isPublicAuthEndpoint)
            {
                var token = await _session.GetItemAsync<string>("token");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                // Boş/invalid token’ı ASLA ekleme
            }

            return await base.SendAsync(request, ct);
        }
    }
}
