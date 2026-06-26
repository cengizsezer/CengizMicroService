namespace SovosService.Api.Application.Models;

// Firmalarım köprüsü için firma bazlı Sovos/entegratör kimlik durumu.
// GÜVENLİK: Şifre ASLA dönmez — sadece HasPassword bool döner
// (Companies.razor / SovosAdminController ile birebir aynı desen).
public sealed class SovosFirmaCredentialDto
{
    public int FirmaId { get; set; }

    // Bu firma için SovosCompanies'te kayıt var mı?
    public bool HasAccount { get; set; }

    // Var olan SovosCompanies kaydının Id'si (şifre değiştirme akışında kullanılır).
    public int? CompanyId { get; set; }

    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";

    // Şifre düz metin dönmez; sadece "tanımlı mı" bilgisi.
    public bool HasPassword { get; set; }

    public bool IsActive { get; set; }
}
