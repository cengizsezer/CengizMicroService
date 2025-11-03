namespace CatalogService.Api.Infrastructure.Accessor
{
    public interface IHttpCurrentTenant
    {
        string CurrentTenantNo { get; }
    }
}
