using CatalogService.Api.Infrastructure.Interface;

namespace CatalogService.Api.Infrastructure.Accessor
{
    public sealed class FixedTenantAccessor : ITenantAccessor
    {
        public FixedTenantAccessor(string? tenantNo) => CurrentTenantNo = tenantNo;
        public string? CurrentTenantNo { get; }
    }
}
