using CatalogService.Api.Infrastructure.Accessor;

namespace CatalogService.Api.Infrastructure.Context
{
    public sealed class DesignTimeTenantAccessor : IHttpCurrentTenant
    {
        private readonly string? _tenant;
        public DesignTimeTenantAccessor(string? tenant = null)
        {
            // MIGRATION_TENANT env ile override edebilirsin.
            _tenant = tenant ?? Environment.GetEnvironmentVariable("MIGRATION_TENANT") ?? "000";
        }
        public string? CurrentTenantNo => _tenant;
    }
}
