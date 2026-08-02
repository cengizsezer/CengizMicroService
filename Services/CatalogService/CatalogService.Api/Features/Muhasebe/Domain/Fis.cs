using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.Muhasebe.Domain
{
    /// <summary>
    /// Yevmiye fişi başlığı. Firma (tenant) + dönem bazında sıralı numaralandırılır.
    /// Kesinleşmiş (Durum=Kesinlesmis) fiş güncellenemez/silinemez.
    /// </summary>
    public class Fis : TenantEntity
    {
        public int Id { get; set; }

        public short DonemYil { get; set; }

        /// <summary>Firma + dönem içinde benzersiz, ör. "2026/000148".</summary>
        public string FisNo { get; set; } = string.Empty;

        public DateTime Tarih { get; set; }

        public FisTuru FisTuru { get; set; }

        public string? BelgeNo { get; set; }

        public string? Aciklama { get; set; }

        public FisKaynak Kaynak { get; set; } = FisKaynak.Manuel;

        public FisDurum Durum { get; set; } = FisDurum.Taslak;

        public int OlusturanId { get; set; }

        public DateTime OlusturmaT { get; set; }

        public DateTime? GuncellemeT { get; set; }

        public ICollection<FisSatir> Satirlar { get; set; } = new List<FisSatir>();
    }
}
