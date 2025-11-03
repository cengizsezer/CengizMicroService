using System.Net.Http.Headers;

namespace CatalogService.Api.Infrastructure.Auth
{
    public class AuthForwardingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _http;

        public AuthForwardingHandler(IHttpContextAccessor http) => _http = http;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var auth = _http.HttpContext?.Request?.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(auth) && AuthenticationHeaderValue.TryParse(auth, out var header))
            {
                request.Headers.Authorization = header; // Bearer ... forward
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
