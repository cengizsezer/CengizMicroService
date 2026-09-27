namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>DB'de saklı hesap kırılım ağacı (okuma; CatalogService'ten gelir).</summary>
    public class FirmaKontrolMizanAgacDto
    {
        /// <summary>0=Onceki, 1=Cari.</summary>
        public int Donem { get; set; }

        public int Yil { get; set; }

        public List<MizanHamDugumDto> Dugumler { get; set; } = new();
    }
}
