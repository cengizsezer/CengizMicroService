using CatalogService.Api.Features.Banka.Domain;

namespace CatalogService.Api.Features.Banka.Dtos
{
    public class HesapDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string FirmaAd { get; set; } = string.Empty;
        public HesapTip Tip { get; set; }
        public string Ad { get; set; } = string.Empty;
        public Siklik Siklik { get; set; }
        public bool AktifMi { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
