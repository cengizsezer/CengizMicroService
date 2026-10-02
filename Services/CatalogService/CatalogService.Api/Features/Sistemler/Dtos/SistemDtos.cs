using CatalogService.Api.Features.Sistemler.Domain;

namespace CatalogService.Api.Features.Sistemler.Dtos
{
    public class SistemDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public SistemTuru Tur { get; set; }
        public bool Aktif { get; set; }

        /// <summary>Atandığı firma sayısı ("DijitalPlanet kullanan firmalarım").</summary>
        public int FirmaSayisi { get; set; }

        /// <summary>Atandığı firmaların adları, alfabetik.</summary>
        public List<string> Firmalar { get; set; } = new();

        /// <summary>Firma işlerinde program olarak seçildiği iş sayısı.</summary>
        public int IsSayisi { get; set; }
    }

    /// <summary>"+ Yeni ekle" ve Yönetim → Sistemler'in ekleme formu.</summary>
    public class SistemEkleDto
    {
        public string Ad { get; set; } = string.Empty;
        public SistemTuru Tur { get; set; }

        /// <summary>
        /// Benzer ad uyarısı gösterildi ve kullanıcı yine de eklemek istiyor. Uyarı SORULMADAN
        /// geçilmez: ilk istek her zaman <c>false</c> gider, sunucu benzer bulursa 409 döner.
        /// </summary>
        public bool Israr { get; set; }
    }

    public class SistemGuncelleDto
    {
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;

        /// <summary>Yeniden adlandırmada benzer ad uyarısına ısrar.</summary>
        public bool Israr { get; set; }
    }

    /// <summary>409 gövdesi: "DijitalPlanet zaten listede, onu mu demek istediniz?"</summary>
    public class SistemBenzerDto
    {
        public string Message { get; set; } = string.Empty;
        public SistemDto Benzer { get; set; } = new();
    }

    public class SistemBirlestirDto
    {
        /// <summary>Kalacak kayıt; kaynak (rotadaki) bunun içine taşınıp silinir.</summary>
        public int HedefId { get; set; }
    }

    public class SistemBirlestirmeSonucuDto
    {
        /// <summary>Hedefe taşınan firma ataması.</summary>
        public int TasinanAtama { get; set; }

        /// <summary>Firma iki kaydı da kullanıyordu: tek atamaya indi (firma kodu korunur).</summary>
        public int BirlesenAtama { get; set; }

        /// <summary>Programı hedefe çevrilen firma işi.</summary>
        public int TasinanIs { get; set; }
    }

    /// <summary>Kartın tek atama satırı.</summary>
    public class FirmaSistemAtamaKaydetDto
    {
        public int SistemId { get; set; }
        public string? FirmaKodu { get; set; }
    }

    /// <summary>
    /// "Kullanılan sistemler" kartının formu: bütün atamalar (gönderilmeyen silinir) + ORKA
    /// firma kodu + mizan formatı + not. ORKA kodu ve mizan formatı Firma kaydının KENDİ
    /// alanlarına yazılır; taşınmadılar.
    /// </summary>
    public class FirmaSistemleriKaydetDto
    {
        public List<FirmaSistemAtamaKaydetDto> Atamalar { get; set; } = new();
        public string? OrkaFirmaKodu { get; set; }
        public string? MizanFormati { get; set; }
        public string? SistemNotu { get; set; }
    }
}
