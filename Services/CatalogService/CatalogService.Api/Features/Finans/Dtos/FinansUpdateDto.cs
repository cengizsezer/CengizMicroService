using System.ComponentModel.DataAnnotations;
using CatalogService.Api.Features.Mukellefler.Domain;

namespace CatalogService.Api.Features.Finans.Dtos
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
