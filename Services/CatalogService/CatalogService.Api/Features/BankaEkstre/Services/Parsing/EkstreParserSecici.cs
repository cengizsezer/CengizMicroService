namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    public interface IEkstreParserSecici
    {
        /// <summary>Banka hesabının ParserTipi'ne karşılık gelen ayrıştırıcı; yoksa null.</summary>
        IEkstreParser? Sec(string? parserTipi);

        /// <summary>Kullanıcıya seçtirilecek ayrıştırıcı listesi.</summary>
        IReadOnlyList<IEkstreParser> Hepsi { get; }
    }

    /// <summary>
    /// Kayıtlı ayrıştırıcıları ParserTipi'ne göre seçer. Yeni banka eklemek için
    /// yalnızca yeni bir <see cref="IEkstreParser"/> DI'a kaydedilir; burası değişmez.
    /// </summary>
    public class EkstreParserSecici : IEkstreParserSecici
    {
        private readonly Dictionary<string, IEkstreParser> _parserlar;

        public EkstreParserSecici(IEnumerable<IEkstreParser> parserlar)
        {
            _parserlar = parserlar.ToDictionary(p => p.ParserTipi, StringComparer.OrdinalIgnoreCase);
            Hepsi = _parserlar.Values.OrderBy(p => p.Ad, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public IReadOnlyList<IEkstreParser> Hepsi { get; }

        public IEkstreParser? Sec(string? parserTipi)
        {
            if (string.IsNullOrWhiteSpace(parserTipi)) return null;
            return _parserlar.TryGetValue(parserTipi.Trim(), out var parser) ? parser : null;
        }
    }
}
