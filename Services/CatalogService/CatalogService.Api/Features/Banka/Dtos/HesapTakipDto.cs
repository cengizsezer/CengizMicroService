using CatalogService.Api.Features.Banka.Domain;

namespace CatalogService.Api.Features.Banka.Dtos
{
    /// <summary>
    /// Banka Takibi aylık görünümü: bir hesap ve istenen ay içinde
    /// "işlendi" işaretli günlerin listesi. Boyama/atlandı hesabı UI'da yapılır.
    /// </summary>
    public class HesapTakipDto
    {
        public int HesapId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public HesapTip Tip { get; set; }
        public Siklik Siklik { get; set; }
        public int FirmaId { get; set; }
        public string FirmaAd { get; set; } = string.Empty;

        // Bu ay içinde IslendiMi=true olan günler.
        public List<DateTime> IslenenGunler { get; set; } = new();

        // Bu hesabın toplam not sayısı (tüm kapsamlar).
        public int NotSayisi { get; set; }

        // Bu ay içinde gün-notu (Kapsam=Gun) olan tarihler.
        public List<DateTime> NotluGunler { get; set; } = new();
    }
}
