using Blazored.SessionStorage;
using System.Net.Http.Headers;

namespace WebApp.Application.Handler
{
    public sealed class AuthHeaderCorridor : DelegatingHandler
    {
        private readonly ISessionStorageService _session;

        public AuthHeaderCorridor(ISessionStorageService session) => _session = session;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await _session.GetItemAsync<string>("token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
