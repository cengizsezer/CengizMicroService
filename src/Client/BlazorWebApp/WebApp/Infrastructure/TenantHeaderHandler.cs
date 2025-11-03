using Blazored.SessionStorage;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Infrastructure
{
    /// <summary>
    /// Seçili tenant bilgisini başlığa koyar (X-Tenant-No).
    /// </summary>
    public sealed class TenantHeaderHandler : DelegatingHandler
    {
        private readonly ISessionStorageService _session;

        public TenantHeaderHandler(ISessionStorageService session) => _session = session;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // Login->firmayı seçtiğinde IdentityService.SelectTenant içinde: await session.SetItemAsync("tenantNo", "201");
            var tenantNo = await _session.GetItemAsync<string>("tenantNo");
            if (!string.IsNullOrWhiteSpace(tenantNo))
            {
                request.Headers.Remove("x-tenant-no");
                request.Headers.Add("x-tenant-no", tenantNo);
            }

            return await base.SendAsync(request, ct);
        }
    }
}
