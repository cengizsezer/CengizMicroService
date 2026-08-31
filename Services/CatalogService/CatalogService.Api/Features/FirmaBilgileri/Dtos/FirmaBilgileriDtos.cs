using CatalogService.Api.Features.FirmaBilgileri.Domain;

namespace CatalogService.Api.Features.FirmaBilgileri.Dtos
{
    /// <summary>
    /// Sicil bölümü. <c>catalog.Firmalar</c>'daki alanlar (unvan, VKN, vergi dairesi,
    /// ticaret sicil no, e-posta, telefon) ile bu modülün alanları (MERSİS, kuruluş,
    /// adres, NACE, sermaye) tek formda birlikte görünüyor; kaydetme ikisini de yazıyor.
    /// </summary>
    public class FirmaSicilDto
    {
        public int FirmaId { get; set; }

        // catalog.Firmalar
        public string Unvan { get; set; } = string.Empty;
        public string VergiKimlikNo { get; set; } = string.Empty;
        public string? VergiDairesi { get; set; }
        public string? TicaretSicilNo { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }

        /// <summary>
        /// ORKA giriş zincirinde F7 sonrası girilen firma kodu (ör. "0001").
        /// <c>catalog.Firmalar</c>'da; ORKA'ya aktarım işi bunsuz kurulmuyor.
        /// </summary>
        public string? OrkaFirmaKodu { get; set; }

        // FirmaSicilBilgisi
        public string? MersisNo { get; set; }
        public DateTime? KurulusTarihi { get; set; }
        public string? Adres { get; set; }
        public string? NaceKodu { get; set; }
        public decimal? Sermaye { get; set; }
        public string? SermayeParaBirimi { get; set; } = "TRY";

        // Mükellefiyet alanları — anasayfadaki firma paneli bunları okuyor, düzenleme
        // burada yapılıyor (KARARLAR §126).
        public string? MukellefiyetTurleri { get; set; }
        public bool? EFatura { get; set; }
        public bool? EDefter { get; set; }
        public DateTime? IseBaslamaTarihi { get; set; }
    }

    public class FirmaOrtakDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? TcknVkn { get; set; }
        public decimal PayTutari { get; set; }
        public decimal PayOrani { get; set; }
        public DateTime? BaslangicTarihi { get; set; }
        public string? Not { get; set; }
        public int Sira { get; set; }
    }

    /// <summary>Ortaklık bölümünün tamamı; toplamlar sunucuda hesaplanır.</summary>
    public class FirmaOrtaklikDto
    {
        public List<FirmaOrtakDto> Ortaklar { get; set; } = new();

        public decimal ToplamPayTutari { get; set; }

        public decimal ToplamPayOrani { get; set; }

        /// <summary>
        /// Toplam pay oranı %100 değil. Kayıt engellenmez — geçiş dönemlerinde tablo
        /// geçici olarak tutmayabiliyor — ama ekran uyarır.
        /// </summary>
        public bool PayOraniUyarisi { get; set; }
    }

    public class FirmaImzaYetkilisiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Tckn { get; set; }
        public string? Gorev { get; set; }
        public TemsilSekli TemsilSekli { get; set; }
        public DateTime? YetkiBaslangic { get; set; }
        public DateTime? YetkiBitis { get; set; }
        public string? Not { get; set; }
        public int Sira { get; set; }

        /// <summary>Yetki süresi dolmuş mu? Ekran bu kaydı görsel olarak ayırır.</summary>
        public bool SuresiDoldu { get; set; }
    }

    public class FirmaBelgesiDto
    {
        public int Id { get; set; }
        public FirmaBelgeTuru Tur { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long Length { get; set; }
        public string? Aciklama { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? YukleyenKullanici { get; set; }
    }

    public class FirmaBelgesiOlusturDto
    {
        public FirmaBelgeTuru Tur { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long Length { get; set; }
        public string? Aciklama { get; set; }
    }
}
