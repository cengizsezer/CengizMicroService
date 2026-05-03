namespace WebApp.Application.RuleEngine.Rules
{
    public class SaticilarBorcBakiyesiKurali : IMizanRule
    {
        public string KuralKodu => "MZ-320-BORC-BAKIYE";

        public IEnumerable<UyariSonucu> Calistir(MizanRuleContext context)
        {
            var saticilar = context.GetCariValue("320");
            if (!saticilar.HasValue) yield break;

            // 320 normalde alacak bakiyesi (negatif/sıfırın altı) verir.
            // Pozitif bakiye = satıcılara avans verilmiş veya hatalı kayıt göstergesidir.
            if (saticilar.Value <= 0) yield break;

            yield return new UyariSonucu(
                Seviye: UyariSeviyesi.Kritik,
                HesapKodu: "320",
                Baslik: "320 Satıcılar hesabı borç bakiyesi veriyor",
                Aciklama: $"320 Satıcılar hesabının cari dönem bakiyesi {saticilar.Value:N2} TL borç bakiyesidir. Beklenen alacak bakiyesi yerine borç bakiyesi olması, mahsup edilmemiş avans veya hatalı kayda işaret eder.",
                CozumOnerisi: "Borç bakiyesi veren satıcı hesaplarını 159 Verilen Sipariş Avansları'na aktarın veya ilgili faturayla mahsup edin. Cari mutabakatlarını gözden geçirin.",
                KuralKodu: KuralKodu);
        }
    }
}
