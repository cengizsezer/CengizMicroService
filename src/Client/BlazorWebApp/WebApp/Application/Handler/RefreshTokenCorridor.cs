using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.SessionStorage;
using WebApp.Domain.Models.User;

namespace WebApp.Application.Handler
{
   

    public sealed class RefreshTokenCorridor : DelegatingHandler
    {
        private readonly ISessionStorageService _session;
        private readonly IHttpClientFactory _factory;

        // Eşzamanlı 401'lerde tek bir select-tenant çağrısı yapılsın diye single-flight kilidi.
        private static readonly SemaphoreSlim _refreshLock = new(1, 1);

        public RefreshTokenCorridor(ISessionStorageService session, IHttpClientFactory factory)
        { _session = session; _factory = factory; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

            var refresh = await _session.GetItemAsync<string>("refresh_token");
            var username = await _session.GetItemAsync<string>("username");
            var tenantNo = await _session.GetItemAsync<string>("tenantNo");
            if (string.IsNullOrWhiteSpace(refresh) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(tenantNo))
                return response;

            // 401 anındaki token; kilidi beklerken başka bir thread yenilemiş olabilir.
            var tokenBefore = await _session.GetItemAsync<string>("token");

            string newToken;
            await _refreshLock.WaitAsync(ct);
            try
            {
                var tokenAfter = await _session.GetItemAsync<string>("token");
                if (!string.IsNullOrWhiteSpace(tokenAfter) && tokenAfter != tokenBefore)
                {
                    // Başka bir istek bu sırada yeniledi; select-tenant çağırma, taze token'ı kullan.
                    newToken = tokenAfter;
                }
                else
                {
                    // taze access token isteği (Authorization gerekmez)
                    // select-tenant dolu access token üretir, refresh token'ı rotate etmez ve [AllowAnonymous]'dur.
                    var bare = _factory.CreateClient("GatewayBare"); // handler’sız
                    var res = await bare.PostAsJsonAsync(
                        "auth/select-tenant",
                        new { UserName = username, TenantNo = tenantNo, RefreshToken = refresh },
                        ct);
                    if (!res.IsSuccessStatusCode) return response;

                    var payload = await res.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: ct);
                    if (payload is null || string.IsNullOrWhiteSpace(payload.Token)) return response;

                    await _session.SetItemAsync("token", payload.Token);
                    newToken = payload.Token;
                }
            }
            finally
            {
                _refreshLock.Release();
            }

            // orijinal isteği 1 kez yeni token ile tekrar et
            var retry = await CloneAsync(request);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
            return await base.SendAsync(retry, ct);
        }

        private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri);

            if (req.Content != null)
            {
                var ms = new MemoryStream();
                await req.Content.CopyToAsync(ms);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);
                foreach (var h in req.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            foreach (var h in req.Headers)
                clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

            clone.Version = req.Version;
            return clone;
        }
    }

}
