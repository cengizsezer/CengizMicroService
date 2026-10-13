using CatalogService.Api.Features.Parolalar.Domain;

namespace CatalogService.Api.Features.Parolalar.Dtos
{
    /// <summary>
    /// Liste satırı. ŞİFRE DEĞERİ YOK — yalnız dolu/boş bilgisi (…Dolu). Değer tek alan için ayrı uçtan istenir
    /// (GET parolalar/{id}/alan/{alan}) ve her istek erişim kaydına yazılır.
    /// </summary>
    public class ParolaDto
    {
        public int Id { get; set; }
        public ParolaTuru Tur { get; set; }

        public int? FirmaId { get; set; }

        /// <summary>Firmalarım'daki güncel ad (kısa ad, yoksa unvan); elle yazılmışsa o ad. "(pasif)" eki istemcide.</summary>
        public string FirmaAdi { get; set; } = string.Empty;

        /// <summary>Firmalarım'da firma pasife alınmış (kayıt silinmez, listede "(pasif)" ile görünür).</summary>
        public bool FirmaPasif { get; set; }

        /// <summary>Firmalarım'da karşılığı yok, ad elle yazılmış.</summary>
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

    /// <summary>
    /// Ekle / düzenle gövdesi. Şifre alanları düz metin gelir, sunucuda şifrelenir; DÜZENLEMEDE boş (null/"") bırakılan
    /// şifre alanı DEĞİŞMEZ. Türe ait olmayan alanlar yok sayılır.
    /// </summary>
    public class ParolaKaydetDto
    {
        public ParolaTuru Tur { get; set; }

        /// <summary>Firmalarım firması (yeni kayıtta aktif olmalı); boşsa <see cref="FirmaAdi"/> elle yazılır.</summary>
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

    /// <summary>Tek şifre alanının değeri (alan ucu). Yanıt önbelleğe alınmaz.</summary>
    public class ParolaAlanDegeriDto
    {
        public string Alan { get; set; } = string.Empty;
        public string Deger { get; set; } = string.Empty;
    }
}
