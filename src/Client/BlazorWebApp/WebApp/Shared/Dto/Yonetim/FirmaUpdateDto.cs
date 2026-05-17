using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Yonetim
{
    public class FirmaUpdateDto
    {
        [Required(ErrorMessage = "VergiKimlikNo zorunludur.")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "VergiKimlikNo 10 karakter olmalıdır.")]
        public string VergiKimlikNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Unvan zorunludur.")]
        [StringLength(250)]
        public string Unvan { get; set; } = string.Empty;

        [Required(ErrorMessage = "KisaAd zorunludur.")]
        [StringLength(100)]
        public string KisaAd { get; set; } = string.Empty;

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "Geçerli bir email girin.")]
        public string Email { get; set; } = string.Empty;

        [StringLength(30)]
        public string Telefon { get; set; } = string.Empty;

        [StringLength(50)]
        public string TicaretSicilNo { get; set; } = string.Empty;

        [StringLength(100)]
        public string VergiDairesi { get; set; } = string.Empty;

        // KDV Beyannamesi (BDP) için ek alanlar — hepsi opsiyonel.
        [StringLength(10)]
        public string? VergiDairesiKodu { get; set; }

        [StringLength(100)]
        public string? YetkiliAdi { get; set; }

        [StringLength(100)]
        public string? YetkiliSoyadi { get; set; }

        [StringLength(10)]
        public string? TelefonAlanKodu { get; set; }

        public int? DuzenleyenId { get; set; }

        public bool Aktif { get; set; }
    }
}
