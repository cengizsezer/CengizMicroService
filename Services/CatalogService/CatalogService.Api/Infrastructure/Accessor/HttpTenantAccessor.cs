using CatalogService.Api.Infrastructure.Interface;

namespace CatalogService.Api.Infrastructure.Accessor
{
    public sealed class HttpTenantAccessor : ITenantAccessor
    {
        private readonly IHttpContextAccessor _ctx;
        public HttpTenantAccessor(IHttpContextAccessor ctx) => _ctx = ctx;

        public string? CurrentTenantNo =>
            _ctx.HttpContext?.Request.Headers.TryGetValue("X-Tenant-Id", out var v) == true
                ? v.ToString()
                : null; // <<< ÖNEMLİ: throw ETME
    }

}
