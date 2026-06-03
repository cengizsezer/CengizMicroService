using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Yonetim
{
    public class HesapUpdateDto
    {
        [Required(ErrorMessage = "Firma zorunludur.")]
        public int FirmaId { get; set; }

        public HesapTip Tip { get; set; }

        [Required(ErrorMessage = "Ad zorunludur.")]
        [StringLength(100)]
        public string Ad { get; set; } = string.Empty;

        public Siklik Siklik { get; set; }

        public bool AktifMi { get; set; }
    }
}
