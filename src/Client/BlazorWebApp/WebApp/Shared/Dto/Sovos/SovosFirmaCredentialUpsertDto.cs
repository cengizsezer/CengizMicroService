using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Sovos;

// Firmalarım köprüsünden upsert (server ile birebir).
// Password: yeni hesap oluştururken zorunlu; mevcut hesapta boş bırakılırsa korunur.
public class SovosFirmaCredentialUpsertDto
{
    [Required(ErrorMessage = "Şirket kısa kodu zorunludur.")]
    public string CompanyCode { get; set; } = "";

    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    public string Username { get; set; } = "";

    public string? Password { get; set; }

    public string? FirmaName { get; set; }

    public bool IsActive { get; set; } = true;
}
