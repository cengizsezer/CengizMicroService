using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.RuleEngine
{
    public class MizanRuleContext
    {
        public required Firma Firma { get; init; }

        public required HesapPlani Mizan { get; init; }

        public required IReadOnlyDictionary<string, decimal?> RawCariValues { get; init; }

        public required IReadOnlyDictionary<string, decimal?> RawOncekiValues { get; init; }

        public MizanEsikler Esikler { get; init; } = MizanEsikler.Default();

        public IEnumerable<MizanSatir> AllAccounts =>
            Mizan.Aktif.Concat(Mizan.Pasif).Concat(Mizan.GelirTablosu)
                 .Where(s => s.Tip == SatirTipi.Account);

        public MizanSatir? FindAccount(string kod) =>
            AllAccounts.FirstOrDefault(s => string.Equals(s.Kod, kod, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<MizanSatir> FindAccountsByPrefix(string prefix) =>
            AllAccounts.Where(s => !string.IsNullOrWhiteSpace(s.Kod) &&
                                   s.Kod.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        public decimal? GetCariValue(string kod)
        {
            if (RawCariValues.TryGetValue(kod, out var v)) return v;
            return FindAccount(kod)?.CariDonem;
        }
    }
}
