using System.ComponentModel.DataAnnotations;

namespace CatalogService.Api.Features.Banka.Dtos
{
    /// <summary>
    /// Bir hesabın bir gününü "işlendi/işlenmedi" olarak işaretler (upsert).
    /// </summary>
    public class IsaretleRequestDto
    {
        [Required]
        public int HesapId { get; set; }

        [Required]
        public DateTime Tarih { get; set; }

        // true → işlendi, false → işareti kaldır.
        public bool IslendiMi { get; set; } = true;
    }
}
