namespace WebApp.Application.RuleEngine.Rules
{
    public class OrtaklardanAlacaklarKurali : IMizanRule
    {
        public string KuralKodu => "MZ-131-ORTAK-ALACAK";

        public IEnumerable<UyariSonucu> Calistir(MizanRuleContext context)
        {
            var bakiye = context.GetCariValue("131");
            if (!bakiye.HasValue || bakiye.Value <= 0) yield break;

            var oran = context.Esikler.AdatFaizOrani;

            yield return new UyariSonucu(
                Seviye: UyariSeviyesi.Kritik,
                HesapKodu: "131",
                Baslik: "Ortaklardan alacaklar tespit edildi",
                Aciklama: $"131 Ortaklardan Alacaklar hesabında {bakiye.Value:N2} TL borç bakiyesi bulunmaktadır. Adat faizi hesaplanması ve KDV beyanı gerekebilir.",
                CozumOnerisi: $"Cari dönem için adat faizi (yıllık %{oran * 100:N2}) hesaplayın, 642-Faiz Gelirleri ve 391-Hesaplanan KDV kayıtlarını yapın. Ayrıca transfer fiyatlandırması açıklamasını beyannameye ekleyin.",
                KuralKodu: KuralKodu);
        }
    }
}
