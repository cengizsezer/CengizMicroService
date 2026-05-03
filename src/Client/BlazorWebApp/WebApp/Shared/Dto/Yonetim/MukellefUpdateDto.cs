using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Yonetim
{
    public class MukellefUpdateDto
    {
        [Required(ErrorMessage = "SozlesmeNo zorunludur.")]
        [StringLength(50)]
        public string SozlesmeNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "SozlesmeTarihi zorunludur.")]
        public DateTime SozlesmeTarihi { get; set; }

        [Required(ErrorMessage = "Unvan zorunludur.")]
        [StringLength(250)]
        public string Unvan { get; set; } = string.Empty;

        [Required(ErrorMessage = "VergiKimlikNo zorunludur.")]
        [StringLength(11, MinimumLength = 10, ErrorMessage = "VergiKimlikNo 10-11 karakter olmalıdır.")]
        public string VergiKimlikNo { get; set; } = string.Empty;

        public MukellefDurumu Durum { get; set; }

        public DateTime? IptalFeshTarihi { get; set; }

        [StringLength(200)]
        public string Hizmet { get; set; } = string.Empty;

        [StringLength(30)]
        public string Telefon { get; set; } = string.Empty;

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "Geçerli bir email girin.")]
        public string? Email { get; set; }
    }
}
