using System.ComponentModel.DataAnnotations;

namespace SovosService.Api.Application.Models;

// Firmalarım köprüsünden gelen upsert isteği.
// Password yalnızca İLK oluşturmada zorunlu; kayıt zaten varken boş gelirse
// mevcut şifre korunur (şifre değişimi ayrı/var olan endpoint ile yapılır).
public sealed class SovosFirmaCredentialUpsertDto
{
    [Required(ErrorMessage = "Şirket kısa kodu zorunludur.")]
    public string CompanyCode { get; set; } = "";

    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    public string Username { get; set; } = "";

    // Yeni kayıt oluşturulurken zorunlu (controller doğrular).
    // Mevcut kayıt güncellenirken boş bırakılırsa şifreye dokunulmaz.
    public string? Password { get; set; }

    // SovosCompanies.Name alanı için — firma ünvanı/kısa adı.
    // Boşsa CompanyCode kullanılır (Name DB'de zorunlu).
    public string? FirmaName { get; set; }

    public bool IsActive { get; set; } = true;
}
