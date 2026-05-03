namespace WebApp.Application.RuleEngine.Rules
{
    public class GelecekYilaAitGiderlerKurali : IMizanRule
    {
        public string KuralKodu => "MZ-180-DEVRE";

        public IEnumerable<UyariSonucu> Calistir(MizanRuleContext context)
        {
            var bakiye = context.GetCariValue("180");
            if (!bakiye.HasValue || bakiye.Value <= 0) yield break;

            yield return new UyariSonucu(
                Seviye: UyariSeviyesi.Uyari,
                HesapKodu: "180",
                Baslik: "180 Gelecek Aylara Ait Giderler bakiyesi var",
                Aciklama: $"180 Gelecek Aylara Ait Giderler hesabı {bakiye.Value:N2} TL bakiye taşıyor. Dönem sonu itibarıyla 180 hesap kapatılmalı, gelecek yıla ait kısımlar 280 hesaba aktarılmalıdır.",
                CozumOnerisi: "180 hesap bakiyesini 280 Gelecek Yıllara Ait Giderler hesabına virmanlayın. Açılış fişi sonrası tekrar 180'e taşıyın.",
                KuralKodu: KuralKodu);
        }
    }
}
