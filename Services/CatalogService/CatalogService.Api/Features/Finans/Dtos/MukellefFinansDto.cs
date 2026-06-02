using CatalogService.Api.Features.Mukellefler.Domain;

namespace CatalogService.Api.Features.Finans.Dtos
{
    public class MukellefFinansDto
    {
        public int Id { get; set; }
        public string SozlesmeNo { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string VergiKimlikNo { get; set; } = string.Empty;
        public MukellefDurumu Durum { get; set; }

        public FinansSinifi? FinansSinifi { get; set; }
        public decimal? AcikBakiye { get; set; }
        public DateTime? SonOdemeTarihi { get; set; }
        public string? FinansAciklama { get; set; }
    }
}
