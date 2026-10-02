namespace WebApp.Shared.Dto.Sistemler
{
    // Sunucudaki CatalogService.Api.Features.Sistemler.Dtos'un aynası; sayısal enum değerleri
    // sözleşmenin parçası.

    public enum SistemTuru : byte
    {
        Muhasebe = 1,
        EFatura = 2,
        Bordro = 3,
        Banka = 4,
        Beyanname = 5,
        Diger = 6
    }

    public class SistemDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public SistemTuru Tur { get; set; }
        public bool Aktif { get; set; }
        public int FirmaSayisi { get; set; }
        public List<string> Firmalar { get; set; } = new();
        public int IsSayisi { get; set; }
    }

    public class SistemEkleDto
    {
        public string Ad { get; set; } = string.Empty;
        public SistemTuru Tur { get; set; }

        /// <summary>Benzer ad uyarısı gösterildi, kullanıcı yine de ekliyor. İlk istek her zaman false.</summary>
        public bool Israr { get; set; }
    }

    public class SistemGuncelleDto
    {
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
        public bool Israr { get; set; }
    }

    public class SistemBenzerDto
    {
        public string Message { get; set; } = string.Empty;
        public SistemDto Benzer { get; set; } = new();
    }

    public class SistemBirlestirDto
    {
        public int HedefId { get; set; }
    }

    public class SistemBirlestirmeSonucuDto
    {
        public int TasinanAtama { get; set; }
        public int BirlesenAtama { get; set; }
        public int TasinanIs { get; set; }
    }

    public class FirmaSistemAtamaKaydetDto
    {
        public int SistemId { get; set; }
        public string? FirmaKodu { get; set; }
    }

    public class FirmaSistemleriKaydetDto
    {
        public List<FirmaSistemAtamaKaydetDto> Atamalar { get; set; } = new();
        public string? OrkaFirmaKodu { get; set; }
        public string? MizanFormati { get; set; }
        public string? SistemNotu { get; set; }
    }

    /// <summary>Kaydetme isteğinin sonucu: kayıt, benzer ad sorusu ya da hata.</summary>
    public sealed record SistemYazmaSonucu(SistemDto? Kayit, SistemDto? Benzer, string? Hata);

    public static class SistemMetin
    {
        public static string Tur(SistemTuru t) => t switch
        {
            SistemTuru.Muhasebe => "Muhasebe",
            SistemTuru.EFatura => "e-Fatura",
            SistemTuru.Bordro => "Bordro",
            SistemTuru.Banka => "Banka portalı",
            SistemTuru.Beyanname => "Beyanname",
            _ => "Diğer"
        };
    }
}
