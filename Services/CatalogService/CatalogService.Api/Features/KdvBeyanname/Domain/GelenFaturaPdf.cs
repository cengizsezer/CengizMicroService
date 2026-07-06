namespace CatalogService.Api.Features.KdvBeyanname.Domain
{
    /// <summary>
    /// Bir faturanın DP portalından canlı çekilip FileApiService'e (MinIO) kaydedilen
    /// PDF'inin eşlemesi. Tekrar çekmeyi (Sovos login maliyetini) önlemek için: aynı
    /// (FirmaId, FaturaNo) kaydı varsa yeniden scrape yapılmadan mevcut FileId döndürülür.
    /// </summary>
    public class GelenFaturaPdf
    {
        public long Id { get; set; }
        public int FirmaId { get; set; }
        public string FaturaNo { get; set; } = string.Empty;

        /// <summary>FileApiService FileRecord.Id — /file/v1/download?id= ile açılır.</summary>
        public int FileId { get; set; }

        public string? FileName { get; set; }
        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
    }
}
