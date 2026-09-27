namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>Bir dönemin saklı hesap kırılım ağacı (okuma).</summary>
    public class FirmaKontrolMizanAgacDto
    {
        /// <summary>0=Onceki, 1=Cari.</summary>
        public int Donem { get; set; }

        public int Yil { get; set; }

        public List<MizanHamDugumDto> Dugumler { get; set; } = new();
    }
}
