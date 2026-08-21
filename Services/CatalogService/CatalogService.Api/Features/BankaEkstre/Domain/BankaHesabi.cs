using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Ekstresi işlenen banka hesabı. Aynı zamanda "banka kayıt defteri" görevi görür:
    /// bankalar arası hareketlerde karşı taraf bu tablodan bulunur (Katman 3).
    /// </summary>
    public class BankaHesabi : TenantEntity
    {
        public int Id { get; set; }

        /// <summary>Ör. "Vakıfbank". Katman 3 metin eşlemesinde kullanılır.</summary>
        public string BankaAdi { get; set; } = string.Empty;

        public HesapTipi HesapTipi { get; set; } = HesapTipi.Vadesiz;

        /// <summary>ISO kodu, ör. "TRY".</summary>
        public string ParaBirimi { get; set; } = "TRY";

        public string? Iban { get; set; }

        /// <summary>ORKA hesap kodu — boşluklu saklanır ve boşluklu yazılır, ör. "102 1 1 01".</summary>
        public string OrkaHesapKodu { get; set; } = string.Empty;

        /// <summary>Hangi parser çalışacak, ör. "VAKIFBANK_VADESIZ".</summary>
        public string ParserTipi { get; set; } = string.Empty;

        public bool Aktif { get; set; } = true;

        /// <summary>
        /// IBAN öğrenme katmanı bu hesapta çalışsın mı? Varsayılan kapalı: kullanıcı IBAN
        /// verisini düzenli tutmuyor ve güvenilir bulmuyor. Katman kod tarafında duruyor,
        /// yalnız bayrakla kapalı — düzenli IBAN gelen bir bankada açılabilsin.
        /// </summary>
        public bool IbanKatmaniAktif { get; set; }

        /// <summary>
        /// VKN öğrenme katmanı bu hesapta çalışsın mı? Varsayılan kapalı: Vakıfbank
        /// ekstresindeki VKN kolonu karşı tarafın değil hesap sahibinin VKN'si (286 satırın
        /// hepsinde aynı değer). Açık kalsaydı ilk onaydan sonra tüm satırlar güven 1.0 ile
        /// aynı hesaba eşleşir, onaya bile düşmezdi. Başka bankada karşı tarafın VKN'si
        /// gerçekten gelebileceği için katman silinmedi.
        /// </summary>
        public bool VknKatmaniAktif { get; set; }
    }
}
