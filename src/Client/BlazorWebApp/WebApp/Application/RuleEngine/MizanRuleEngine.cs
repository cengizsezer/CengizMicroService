namespace WebApp.Application.RuleEngine
{
    public class MizanRuleEngine
    {
        private readonly IEnumerable<IMizanRule> _rules;

        public MizanRuleEngine(IEnumerable<IMizanRule> rules)
        {
            _rules = rules;
        }

        public IReadOnlyList<UyariSonucu> Calistir(MizanRuleContext context)
        {
            var sonuclar = new List<UyariSonucu>();

            foreach (var rule in _rules)
            {
                IEnumerable<UyariSonucu> uyarilar;
                try
                {
                    uyarilar = rule.Calistir(context) ?? Enumerable.Empty<UyariSonucu>();
                }
                catch
                {
                    continue;
                }

                foreach (var u in uyarilar)
                {
                    if (u is null) continue;
                    sonuclar.Add(u);
                }
            }

            return sonuclar
                .OrderByDescending(u => u.Seviye)
                .ThenBy(u => u.HesapKodu, StringComparer.Ordinal)
                .ToList();
        }
    }
}
