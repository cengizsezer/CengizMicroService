using Blazored.LocalStorage;
using Blazored.SessionStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Extensions;

namespace WebApp.Infrastructure
{
    public sealed class AuthTokenHandler : DelegatingHandler
    {
        private readonly ISessionStorageService _session;

        public AuthTokenHandler(ISessionStorageService session) => _session = session;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            // Bu endpoint’ler anonim; token ekleme.
            bool isPublicAuthEndpoint =
                path.Equals("/auth/login", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/auth/register", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/auth/select-tenant", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/auth/refresh-token", StringComparison.OrdinalIgnoreCase);

            if (!isPublicAuthEndpoint)
            {
                var token = await _session.GetItemAsync<string>("token");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            return await base.SendAsync(request, ct);
        }
    }
    //public class AuthTokenHandler : DelegatingHandler
    //{
    //    private readonly ISyncLocalStorageService storageService;

    //    public AuthTokenHandler(ISyncLocalStorageService identityService)
    //    {
    //        this.storageService = identityService;
    //    }

    //    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    //    {
    //        if (storageService != null)
    //        {
    //            string token = storageService.GetToken();
    //            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
    //        }

    //        return base.SendAsync(request, cancellationToken);
    //    }
    //}
}