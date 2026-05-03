namespace WebApp.Application.RuleEngine.Rules
{
    public class DevredenKdvKurali : IMizanRule
    {
        public string KuralKodu => "MZ-190-DEVREDEN-KDV";

        public IEnumerable<UyariSonucu> Calistir(MizanRuleContext context)
        {
            var devredenKdv = context.GetCariValue("190");
            if (!devredenKdv.HasValue || devredenKdv.Value <= 0) yield break;

            yield return new UyariSonucu(
                Seviye: UyariSeviyesi.Bilgi,
                HesapKodu: "190",
                Baslik: "190 Devreden KDV bakiyesi mevcut",
                Aciklama: $"190 Devreden KDV hesabında {devredenKdv.Value:N2} TL bakiye var. Aralık ayı KDV beyannamesindeki devir tutarı ile uyumlu olduğu doğrulanmalıdır.",
                CozumOnerisi: "Aralık KDV beyannamesinde sonraki döneme devreden KDV tutarını 190 hesap bakiyesi ile karşılaştırın. Düzeltme beyanı verilmişse mizan da güncellenmelidir.",
                KuralKodu: KuralKodu);
        }
    }
}
