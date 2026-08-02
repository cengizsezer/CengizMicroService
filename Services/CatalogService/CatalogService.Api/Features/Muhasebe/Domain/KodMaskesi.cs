using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.Muhasebe.Domain
{
    /// <summary>
    /// Firma (tenant) başına hesap kodu maskesi. Segment uzunlukları ve ayraç
    /// buradan okunur; kod üretme/doğrulama mantığı hiçbir yerde sabit yazmaz.
    /// Varsayılan maske "3,2,2,4" ve ayraç ".".
    /// </summary>
    public class KodMaskesi : TenantEntity
    {
        public int Id { get; set; }

        /// <summary>Virgülle ayrılmış segment uzunlukları, ör. "3,2,2,4".</summary>
        public string SegmentUzunluk { get; set; } = "3,2,2,4";

        /// <summary>Segmentleri ayıran karakter, ör. ".".</summary>
        public string Ayrac { get; set; } = ".";
    }
}
