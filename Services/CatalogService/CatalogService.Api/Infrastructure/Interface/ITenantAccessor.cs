namespace CatalogService.Api.Infrastructure.Interface
{
    public interface ITenantAccessor
    {
        string CurrentTenantNo { get; }
    }
}
