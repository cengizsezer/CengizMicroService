namespace CatalogService.Api.Features.MevzuatGrafigi.Domain
{
    /// <summary>
    /// Önizlemedeki "takip edilecek şartlar" işareti: bir Firma kartı için bir Şart kartı
    /// tamamlandı mı. (FirmaKartId, SartKartId) birincil anahtardır.
    /// </summary>
    public class MevzuatSartTakip
    {
        public int FirmaKartId { get; set; }
        public int SartKartId { get; set; }
        public bool Tamamlandi { get; set; }
    }
}
