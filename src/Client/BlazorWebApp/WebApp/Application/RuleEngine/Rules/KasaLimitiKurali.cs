namespace WebApp.Application.RuleEngine.Rules
{
    public class KasaLimitiKurali : IMizanRule
    {
        public string KuralKodu => "MZ-100-KASA-LIMIT";

        public IEnumerable<UyariSonucu> Calistir(MizanRuleContext context)
        {
            var kasa = context.GetCariValue("100");
            if (!kasa.HasValue) yield break;

            var limit = context.Esikler.KasaLimiti;
            if (kasa.Value <= limit) yield break;

            yield return new UyariSonucu(
                Seviye: UyariSeviyesi.Uyari,
                HesapKodu: "100",
                Baslik: "Kasa bakiyesi olağandan yüksek",
                Aciklama: $"100 Kasa hesabının cari dönem bakiyesi {kasa.Value:N2} TL olup belirlenen {limit:N2} TL eşiğini aşmaktadır.",
                CozumOnerisi: "Kasa hareketlerini gözden geçirin; eksiye düşen günler ve büyük tutarlı çıkışlar için açıklayıcı belge temin edin. Gerekirse ortaklara borç (131) hesabına aktarım değerlendirin.",
                KuralKodu: KuralKodu);
        }
    }
}
