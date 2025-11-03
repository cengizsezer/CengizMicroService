namespace CatalogService.Api.Infrastructure.Accessor
{
    public sealed class FixedTenantAccessor : IHttpCurrentTenant
    {
        public FixedTenantAccessor(string? tenantNo) => CurrentTenantNo = tenantNo;
        public string? CurrentTenantNo { get; }
    }
}
