using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.Muhasebe.Domain
{
    /// <summary>Masraf/maliyet merkezi. Firma (tenant) bazlıdır.</summary>
    public class MasrafMerkezi : TenantEntity
    {
        public int Id { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
    }
}
