using CatalogService.Api.Core.Base;

namespace CatalogService.Api.Core.Domain
{
    public class AccountingCode: TenantEntity
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty; // Hesap Kodu, örn: 740.01.01.00001
        public string Description { get; set; } = string.Empty; // Açıklama, örn: BRÜT ÜCRET GİD.
    }
}
