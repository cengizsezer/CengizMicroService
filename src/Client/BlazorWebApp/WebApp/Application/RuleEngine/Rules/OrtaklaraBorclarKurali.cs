namespace WebApp.Application.RuleEngine.Rules
{
    public class OrtaklaraBorclarKurali : IMizanRule
    {
        public string KuralKodu => "MZ-331-ORTAK-BORC";

        public IEnumerable<UyariSonucu> Calistir(MizanRuleContext context)
        {
            var bakiye = context.GetCariValue("331");
            if (!bakiye.HasValue || bakiye.Value >= 0) yield break;

            var mutlak = Math.Abs(bakiye.Value);

            yield return new UyariSonucu(
                Seviye: UyariSeviyesi.Uyari,
                HesapKodu: "331",
                Baslik: "Ortaklara borçlar bakiyesi yüksek",
                Aciklama: $"331 Ortaklara Borçlar hesabında {mutlak:N2} TL alacak bakiyesi bulunmaktadır. Örtülü sermaye sınırının aşılıp aşılmadığı kontrol edilmelidir.",
                CozumOnerisi: "Şirket öz sermayesinin 3 katını aşan ortak borçlarını örtülü sermaye olarak değerlendirin; varsa faiz ve kur farklarını KKEG yapın.",
                KuralKodu: KuralKodu);
        }
    }
}
