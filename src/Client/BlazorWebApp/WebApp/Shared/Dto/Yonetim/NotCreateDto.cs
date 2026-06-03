using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Yonetim
{
    public class NotCreateDto
    {
        public int HesapId { get; set; }

        public NotKapsam Kapsam { get; set; } = NotKapsam.Genel;

        public DateTime? Tarih { get; set; }
        public int? Yil { get; set; }
        public int? Ay { get; set; }

        [Required(ErrorMessage = "Not metni zorunludur.")]
        [StringLength(2000)]
        public string Metin { get; set; } = string.Empty;

        public bool Sabit { get; set; }
    }
}
