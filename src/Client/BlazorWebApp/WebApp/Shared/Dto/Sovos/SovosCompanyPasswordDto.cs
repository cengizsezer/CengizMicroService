using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Sovos;

public class SovosCompanyPasswordDto
{
    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MinLength(4, ErrorMessage = "Şifre en az 4 karakter olmalıdır.")]
    public string NewPassword { get; set; } = "";
}
