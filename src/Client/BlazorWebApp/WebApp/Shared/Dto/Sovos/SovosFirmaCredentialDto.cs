namespace WebApp.Shared.Dto.Sovos;

// Firmalarım köprüsü — firma bazlı entegratör kimlik durumu (server ile birebir).
// GÜVENLİK: Şifre alanı yok; sadece HasPassword bool.
public class SovosFirmaCredentialDto
{
    public int FirmaId { get; set; }
    public bool HasAccount { get; set; }
    public int? CompanyId { get; set; }
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public bool HasPassword { get; set; }
    public bool IsActive { get; set; }
}
