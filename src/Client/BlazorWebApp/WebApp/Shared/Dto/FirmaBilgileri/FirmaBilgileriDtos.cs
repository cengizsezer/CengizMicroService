namespace WebApp.Shared.Dto.FirmaBilgileri
{
    /// <summary>Yetkilinin firmayı tek başına mı yoksa birlikte mi temsil ettiği.</summary>
    public enum TemsilSekli : byte
    {
        Munferit = 1,
        Musterek = 2
    }

    /// <summary>Firmaya bağlanabilen belge türleri; sayısal değerler sunucuyla ortak.</summary>
    public enum FirmaBelgeTuru : byte
    {
        ImzaSirkuleri = 1,
        VergiLevhasi = 2,
        FaaliyetBelgesi = 3,
        TicaretSicilGazetesi = 4,
        Diger = 9
    }

    /// <summary>
    /// Sicil bölümü: <c>catalog.Firmalar</c>'daki alanlar ile Firma Bilgileri modülünün
    /// kendi alanları tek formda. Kaydetme ikisini de yazar.
    /// </summary>
    public class FirmaSicilDto
    {
        public int FirmaId { get; set; }

        public string Unvan { get; set; } = string.Empty;
        public string VergiKimlikNo { get; set; } = string.Empty;
        public string? VergiDairesi { get; set; }
        public string? TicaretSicilNo { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }

        /// <summary>ORKA giriş zincirinde F7 sonrası girilen firma kodu (ör. "0001").</summary>
        public string? OrkaFirmaKodu { get; set; }

        public string? MersisNo { get; set; }
        public DateTime? KurulusTarihi { get; set; }
        public string? Adres { get; set; }
        public string? NaceKodu { get; set; }
        public decimal? Sermaye { get; set; }
        public string? SermayeParaBirimi { get; set; } = "TRY";

        // Mükellefiyet alanları — anasayfadaki firma paneli okur, düzenleme burada.
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

    public class FirmaOrtaklikDto
    {
        public List<FirmaOrtakDto> Ortaklar { get; set; } = new();
        public decimal ToplamPayTutari { get; set; }
        public decimal ToplamPayOrani { get; set; }

        /// <summary>Toplam pay oranı %100 değil; kayıt engellenmez, ekran uyarır.</summary>
        public bool PayOraniUyarisi { get; set; }
    }

    public class FirmaImzaYetkilisiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Tckn { get; set; }
        public string? Gorev { get; set; }
        public TemsilSekli TemsilSekli { get; set; } = TemsilSekli.Munferit;
        public DateTime? YetkiBaslangic { get; set; }
        public DateTime? YetkiBitis { get; set; }
        public string? Not { get; set; }
        public int Sira { get; set; }

        /// <summary>Sunucuda hesaplanır; istemcinin saatine bırakılmaz.</summary>
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
