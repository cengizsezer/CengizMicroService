using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;

namespace CatalogService.Api.Features.Anasayfa.Dtos
{
    /// <summary>
    /// Firma satırında gösterilen uyarı türü. Kullanıcı firmaya tıklamadan, listedeki
    /// simgeden sorunun ne olduğunu görebilsin diye tür ayrı taşınıyor — ekran türe göre
    /// simge/renk seçiyor, metni sunucudan olduğu gibi yazıyor.
    /// </summary>
    public enum FirmaUyariTuru : byte
    {
        ImzaYetkisiBitiyor = 1,
        PayOraniTutmuyor = 2,
        EksikSicilAlani = 3
    }

    public class FirmaUyariDto
    {
        public FirmaUyariTuru Tur { get; set; }

        /// <summary>Kullanıcıya gösterilen cümle; istemci kendi metnini üretmiyor.</summary>
        public string Mesaj { get; set; } = string.Empty;
    }

    /// <summary>Sol listedeki bir firma satırı.</summary>
    public class FirmaPaneliOzetDto
    {
        public int FirmaId { get; set; }

        /// <summary>Kısa ad varsa o, yoksa unvan — listede okunabilir olan.</summary>
        public string Ad { get; set; } = string.Empty;

        public string Unvan { get; set; } = string.Empty;

        public string VergiKimlikNo { get; set; } = string.Empty;

        public List<FirmaUyariDto> Uyarilar { get; set; } = new();

        public bool UyariVar => Uyarilar.Count > 0;
    }

    /// <summary>Mükellefiyet bölümü. Alanların bir kısmı <c>catalog.Firmalar</c>'dan.</summary>
    public class FirmaMukellefiyetDto
    {
        public string VergiKimlikNo { get; set; } = string.Empty;
        public string? VergiDairesi { get; set; }
        public string? MukellefiyetTurleri { get; set; }
        public bool? EFatura { get; set; }
        public bool? EDefter { get; set; }
        public DateTime? IseBaslamaTarihi { get; set; }
        public string? NaceKodu { get; set; }
    }

    /// <summary>Sicil bölümü. Okuma odaklı; düzenleme Firma Bilgileri ekranında.</summary>
    public class FirmaPaneliSicilDto
    {
        public string? TicaretSicilNo { get; set; }
        public string? MersisNo { get; set; }
        public decimal? Sermaye { get; set; }
        public string? SermayeParaBirimi { get; set; }
        public DateTime? KurulusTarihi { get; set; }
        public string? Adres { get; set; }

        /// <summary>
        /// ORKA giriş zincirinde F7 sonrası girilen firma kodu. Panelde okunuyor;
        /// ORKA'ya aktarım işi bunsuz kurulmadığı için eksikliği burada da görülsün diye.
        /// </summary>
        public string? OrkaFirmaKodu { get; set; }
    }

    /// <summary>
    /// İmza yetkilisi satırı — panelin kendi görünümü.
    ///
    /// Düzenleme ekranının <see cref="FirmaImzaYetkilisiDto"/>'su kullanılmadı: orası bir
    /// <b>istek</b> gövdesi (PUT ile geri gönderiliyor), burada gereken ise ekranda
    /// yazılacak <see cref="KalanGun"/> gibi türetilmiş alanlar.
    /// </summary>
    public class FirmaPaneliYetkiliDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Tckn { get; set; }
        public string? Gorev { get; set; }
        public TemsilSekli TemsilSekli { get; set; }
        public DateTime? YetkiBitis { get; set; }

        /// <summary>Bitişe kaç gün kaldı; negatifse dolmuş. Bitiş boşsa <c>null</c> (süresiz).</summary>
        public int? KalanGun { get; set; }

        public bool SuresiDoldu { get; set; }
    }

    /// <summary>Sağ paneldeki seçili firmanın tamamı.</summary>
    public class FirmaPaneliDetayDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;

        public FirmaMukellefiyetDto Mukellefiyet { get; set; } = new();
        public FirmaPaneliSicilDto Sicil { get; set; } = new();

        /// <summary>
        /// Ortaklık tablosu ve toplamları. Düzenleme ekranıyla <b>aynı</b> DTO ve aynı
        /// hesap (<c>FirmaBilgiService.Ortaklik</c>) kullanılıyor: %100 uyarısı iki ekranda
        /// farklı çıkamaz.
        /// </summary>
        public FirmaOrtaklikDto Ortaklik { get; set; } = new();

        public List<FirmaPaneliYetkiliDto> Yetkililer { get; set; } = new();

        /// <summary>Belge listesi düzenleme ekranıyla aynı DTO; görüntüleme de aynı altyapı.</summary>
        public List<FirmaBelgesiDto> Belgeler { get; set; } = new();

        public List<FirmaUyariDto> Uyarilar { get; set; } = new();
    }

    /// <summary>
    /// Anasayfa firma panelinin tek çağrılık yanıtı: <b>bütün</b> firmaların liste
    /// satırları (uyarılarıyla) + seçili firmanın ayrıntısı. Ekran açılırken firma başına
    /// ayrı istek atılmıyor.
    /// </summary>
    public class FirmaPaneliDto
    {
        public List<FirmaPaneliOzetDto> Firmalar { get; set; } = new();

        /// <summary>Firma yoksa <c>null</c>.</summary>
        public FirmaPaneliDetayDto? Secili { get; set; }
    }
}
