namespace WebApp.Application.RuleEngine
{
    public interface IMizanRule
    {
        string KuralKodu { get; }

        IEnumerable<UyariSonucu> Calistir(MizanRuleContext context);
    }
}
