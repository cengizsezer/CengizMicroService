using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Yonetim
{
    public class FinansUpdateDto
    {
        public FinansSinifi? FinansSinifi { get; set; }
        public decimal? AcikBakiye { get; set; }
        public DateTime? SonOdemeTarihi { get; set; }

        [StringLength(1000)]
        public string? FinansAciklama { get; set; }
    }
}
