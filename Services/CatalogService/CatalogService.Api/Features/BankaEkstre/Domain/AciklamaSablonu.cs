namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// İşlem tipi → muhasebe açıklaması şablonu. Kod içine gömülmez; yeni banka
    /// eklenirken yalnız bu tabloya satır eklenir. Banka bazlıdır (<see cref="ParserTipi"/>).
    /// Şablondaki yer tutucular: {UNVAN} {BANKA} {HESAP} {PLAKA} {VERGI}.
    /// </summary>
    public class AciklamaSablonu
    {
        public int Id { get; set; }

        /// <summary>Ör. "VAKIFBANK_VADESIZ".</summary>
        public string ParserTipi { get; set; } = string.Empty;

        /// <summary>İşlem tipiyle eşleşecek metin veya regex.</summary>
        public string IslemTipiDeseni { get; set; } = string.Empty;

        public EslesmeTuru EslesmeTuru { get; set; } = EslesmeTuru.Tam;

        /// <summary>Ör. "Gelen Eft - {UNVAN}".</summary>
        public string Sablon { get; set; } = string.Empty;

        /// <summary>
        /// İşlem bankalar arası mı (virman, süpürme, hesaplar arası EFT)?
        /// Katman 3 (banka kayıt defteri) yalnız bu satırlarda denenir ve
        /// unvan yerine banka adı kullanılır.
        /// </summary>
        public bool BankalarArasi { get; set; }

        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
