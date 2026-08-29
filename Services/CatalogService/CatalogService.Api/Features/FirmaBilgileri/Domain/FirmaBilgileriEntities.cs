namespace CatalogService.Api.Features.FirmaBilgileri.Domain
{
    /// <summary>
    /// Firmanın sicil bilgilerinden <b>catalog.Firmalar'da olmayanlar</b>.
    ///
    /// Unvan, VKN, vergi dairesi, ticaret sicil no, e-posta ve telefon zaten
    /// <c>Firma</c> kaydında duruyor; buraya kopyalanmadı. İki tabloda aynı alanı tutmak,
    /// birini güncelleyip diğerini unutmanın kapısını açardı — ekran ikisini birlikte
    /// gösteriyor ve tek kaydetmede ikisini de yazıyor (bkz. KARARLAR §93).
    ///
    /// Firma başına tek kayıt (<see cref="FirmaId"/> benzersiz).
    /// </summary>
    public class FirmaSicilBilgisi
    {
        public int Id { get; set; }

        /// <summary><c>catalog.Firmalar.Id</c>. Modülün kapsamı bu değer.</summary>
        public int FirmaId { get; set; }

        public string? MersisNo { get; set; }

        public DateTime? KurulusTarihi { get; set; }

        public string? Adres { get; set; }

        /// <summary>NACE faaliyet kodu, ör. "69.20.01".</summary>
        public string? NaceKodu { get; set; }

        /// <summary>Kayıtlı sermaye. Para birimi ayrı alanda; varsayılan TL.</summary>
        public decimal? Sermaye { get; set; }

        public string? SermayeParaBirimi { get; set; } = "TRY";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Firmanın ortağı. Pay oranı yüzde olarak tutulur; toplamın %100 olup olmadığı
    /// <b>uyarı</b>dır, kayıt engeli değil: geçiş dönemlerinde (pay devri, sermaye artışı)
    /// tablo geçici olarak tutmayabiliyor.
    /// </summary>
    public class FirmaOrtak
    {
        public int Id { get; set; }

        public int FirmaId { get; set; }

        public string Ad { get; set; } = string.Empty;

        /// <summary>Gerçek kişide TCKN (11 hane), tüzel kişide VKN (10 hane).</summary>
        public string? TcknVkn { get; set; }

        public decimal PayTutari { get; set; }

        /// <summary>Yüzde; 0–100.</summary>
        public decimal PayOrani { get; set; }

        public DateTime? BaslangicTarihi { get; set; }

        public string? Not { get; set; }

        public int Sira { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>Yetkilinin firmayı tek başına mı yoksa birlikte mi temsil ettiği.</summary>
    public enum TemsilSekli : byte
    {
        Munferit = 1,
        Musterek = 2
    }

    /// <summary>
    /// İmza yetkilisi. Yetki süresi dolmuş kayıt <b>silinmez</b>: geçmişe dönük belge
    /// kontrolünde kimin ne zaman yetkili olduğu gerekiyor. Ekran süresi dolanı görsel
    /// olarak ayırıyor.
    /// </summary>
    public class FirmaImzaYetkilisi
    {
        public int Id { get; set; }

        public int FirmaId { get; set; }

        public string Ad { get; set; } = string.Empty;

        public string? Tckn { get; set; }

        /// <summary>Görev/unvan, ör. "Yönetim Kurulu Başkanı".</summary>
        public string? Gorev { get; set; }

        public TemsilSekli TemsilSekli { get; set; } = TemsilSekli.Munferit;

        public DateTime? YetkiBaslangic { get; set; }

        /// <summary>Boşsa süresiz.</summary>
        public DateTime? YetkiBitis { get; set; }

        public string? Not { get; set; }

        public int Sira { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>Firmaya bağlanabilen belge türleri.</summary>
    public enum FirmaBelgeTuru : byte
    {
        ImzaSirkuleri = 1,
        VergiLevhasi = 2,
        FaaliyetBelgesi = 3,
        TicaretSicilGazetesi = 4,
        Diger = 9
    }

    /// <summary>
    /// Firma belgesi (imza sirküleri, vergi levhası, faaliyet belgesi…).
    ///
    /// Dosyanın kendisi FileApiService'te; burada FileId + metadata. Beyanname ekleriyle
    /// <b>aynı altyapı</b>, tek farkla: burada aynı türden birden çok belge olabilir —
    /// vergi levhası her yıl yenileniyor ve eskisi kayıtta kalmalı.
    /// </summary>
    public class FirmaBelgesi
    {
        public int Id { get; set; }

        public int FirmaId { get; set; }

        public FirmaBelgeTuru Tur { get; set; }

        public int FileId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/pdf";

        public long Length { get; set; }

        /// <summary>Belgeyi ayırt eden serbest metin, ör. "2026 yılı".</summary>
        public string? Aciklama { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? YukleyenKullanici { get; set; }
    }
}
