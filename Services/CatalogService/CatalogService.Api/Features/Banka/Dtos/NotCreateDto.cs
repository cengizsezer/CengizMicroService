using System.ComponentModel.DataAnnotations;
using CatalogService.Api.Features.Banka.Domain;

namespace CatalogService.Api.Features.Banka.Dtos
{
    public class NotCreateDto
    {
        [Required]
        public int HesapId { get; set; }

        public NotKapsam Kapsam { get; set; } = NotKapsam.Genel;

        // Kapsam=Gun ise dolu.
        public DateTime? Tarih { get; set; }

        // Kapsam=Ay ise Yil + Ay dolu.
        public int? Yil { get; set; }
        public int? Ay { get; set; }

        [Required(ErrorMessage = "Not metni zorunludur.")]
        [StringLength(2000)]
        public string Metin { get; set; } = string.Empty;

        public bool Sabit { get; set; }
    }
}
