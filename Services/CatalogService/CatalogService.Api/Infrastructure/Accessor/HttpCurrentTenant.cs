namespace CatalogService.Api.Infrastructure.Accessor
{
    public sealed class HttpCurrentTenant : IHttpCurrentTenant
    {
        private readonly IHttpContextAccessor _ctx;
        public HttpCurrentTenant(IHttpContextAccessor ctx) => _ctx = ctx;

        public string? CurrentTenantNo
        {
            get
            {
                var http = _ctx.HttpContext;
                if (http is null) return null;

                // 1) JWT claim: tn
                var fromClaim = http.User?.FindFirst("tn")?.Value;
                if (!string.IsNullOrWhiteSpace(fromClaim))
                    return fromClaim;

                // 2) Header'lar (case-insensitive ama ismen farklı olabilir)
                if (http.Request.Headers.TryGetValue("x-tenant-no", out var v) && !string.IsNullOrWhiteSpace(v))
                    return v.ToString();

                if (http.Request.Headers.TryGetValue("X-Tenant-No", out v) && !string.IsNullOrWhiteSpace(v))
                    return v.ToString();

                // 3) Eski isim desteği (varsa)
                if (http.Request.Headers.TryGetValue("X-Tenant-Id", out v) && !string.IsNullOrWhiteSpace(v))
                    return v.ToString();

                return null;
            }
        }
    }

}
