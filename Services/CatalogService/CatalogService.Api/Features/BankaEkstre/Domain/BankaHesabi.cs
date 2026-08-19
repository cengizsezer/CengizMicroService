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
    }
}
