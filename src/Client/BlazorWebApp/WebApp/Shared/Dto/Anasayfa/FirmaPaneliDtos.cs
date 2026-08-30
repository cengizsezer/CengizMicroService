using WebApp.Shared.Dto.FirmaBilgileri;

namespace WebApp.Shared.Dto.Anasayfa
{
    /// <summary>Uyarı türü; sayısal değerler sunucuyla ortak. Ekran türe göre simge seçer.</summary>
    public enum FirmaUyariTuru : byte
    {
        ImzaYetkisiBitiyor = 1,
        PayOraniTutmuyor = 2,
        EksikSicilAlani = 3
    }

    public class FirmaUyariDto
    {
        public FirmaUyariTuru Tur { get; set; }

        /// <summary>Sunucunun cümlesi olduğu gibi gösterilir; istemci kendi metnini üretmez.</summary>
        public string Mesaj { get; set; } = string.Empty;
    }

    /// <summary>Sol listedeki firma satırı.</summary>
    public class FirmaPaneliOzetDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string VergiKimlikNo { get; set; } = string.Empty;
        public List<FirmaUyariDto> Uyarilar { get; set; } = new();

        public bool UyariVar => Uyarilar.Count > 0;
    }

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

    public class FirmaPaneliSicilDto
    {
        public string? TicaretSicilNo { get; set; }
        public string? MersisNo { get; set; }
        public decimal? Sermaye { get; set; }
        public string? SermayeParaBirimi { get; set; }
        public DateTime? KurulusTarihi { get; set; }
        public string? Adres { get; set; }
    }

    public class FirmaPaneliYetkiliDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Tckn { get; set; }
        public string? Gorev { get; set; }
        public TemsilSekli TemsilSekli { get; set; }
        public DateTime? YetkiBitis { get; set; }

        /// <summary>Bitişe kalan gün; negatifse dolmuş, <c>null</c> ise süresiz.</summary>
        public int? KalanGun { get; set; }

        public bool SuresiDoldu { get; set; }
    }

    public class FirmaPaneliDetayDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;

        public FirmaMukellefiyetDto Mukellefiyet { get; set; } = new();
        public FirmaPaneliSicilDto Sicil { get; set; } = new();
        public FirmaOrtaklikDto Ortaklik { get; set; } = new();
        public List<FirmaPaneliYetkiliDto> Yetkililer { get; set; } = new();
        public List<FirmaBelgesiDto> Belgeler { get; set; } = new();
        public List<FirmaUyariDto> Uyarilar { get; set; } = new();
    }

    /// <summary>
    /// Firma panelinin tek çağrılık yanıtı: tüm firmaların satırları + seçili firmanın
    /// ayrıntısı. Ekran açılışta firma başına ayrı istek atmıyor.
    /// </summary>
    public class FirmaPaneliDto
    {
        public List<FirmaPaneliOzetDto> Firmalar { get; set; } = new();
        public FirmaPaneliDetayDto? Secili { get; set; }
    }
}
