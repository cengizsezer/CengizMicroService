namespace WebApp.Shared.Dto.Parolalar
{
    // Sunucudaki CatalogService.Api.Features.Parolalar.Dtos'un aynası (JSON camelCase; enum sayı olarak).

    public enum ParolaTuru : byte
    {
        Entegrator = 1,
        DijitalVergiDairesi = 2,
        Sgk = 3
    }

    /// <summary>Liste satırı — ŞİFRE DEĞERİ YOK, yalnız dolu/boş. Değer tek alan için ayrı uçtan, erişim kaydıyla.</summary>
    public class ParolaDto
    {
        public int Id { get; set; }
        public ParolaTuru Tur { get; set; }
        public int? FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public bool FirmaPasif { get; set; }
        public bool ElleYazilan { get; set; }
        public string? Entegrator { get; set; }
        public string? PortalAdresi { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;
        public string? KullaniciKoduEk { get; set; }
        public string? Sube { get; set; }
        public string? Not { get; set; }
        public bool SifreDolu { get; set; }
        public bool ParolaDolu { get; set; }
        public bool SistemSifresiDolu { get; set; }
        public bool IsyeriSifresiDolu { get; set; }
        public DateTime GuncellemeTarihi { get; set; }
        public string? GuncelleyenKullanici { get; set; }
    }

    /// <summary>Ekle / düzenle gövdesi. Düzenlemede boş şifre alanı değişmez.</summary>
    public class ParolaKaydetDto
    {
        public ParolaTuru Tur { get; set; }
        public int? FirmaId { get; set; }
        public string? FirmaAdi { get; set; }
        public string? Entegrator { get; set; }
        public string? PortalAdresi { get; set; }
        public string? KullaniciAdi { get; set; }
        public string? KullaniciKoduEk { get; set; }
        public string? Sube { get; set; }
        public string? Not { get; set; }
        public string? Sifre { get; set; }
        public string? Parola { get; set; }
        public string? SistemSifresi { get; set; }
        public string? IsyeriSifresi { get; set; }
    }

    public class ParolaAlanDegeriDto
    {
        public string Alan { get; set; } = string.Empty;
        public string Deger { get; set; } = string.Empty;
    }
}
