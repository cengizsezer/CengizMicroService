namespace CatalogService.Api.Core.Base
{
    // Domain/Common/TenantEntity.cs
    public abstract class TenantEntity
    {
        // Firma no (ör: "201","106"…)
        public string TenantNo { get; set; } = default!;
    }

}
