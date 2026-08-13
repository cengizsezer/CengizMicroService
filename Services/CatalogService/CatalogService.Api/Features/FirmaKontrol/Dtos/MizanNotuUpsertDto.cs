namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>
    /// Mizan hesap notu yazma isteği. (FirmaId, HesapKodu, DonemYili) tekil olduğundan
    /// aynı üçlü için ikinci çağrı mevcut notu günceller.
    /// </summary>
    public class MizanNotuUpsertDto
    {
        public string HesapKodu { get; set; } = string.Empty;
        public string Metin { get; set; } = string.Empty;

        /// <summary>0=Açıklama, 1=Düzeltilecek.</summary>
        public int NotTuru { get; set; }

        /// <summary>null = kalıcı not.</summary>
        public int? DonemYili { get; set; }

        public bool UyariBastir { get; set; }
    }
}
