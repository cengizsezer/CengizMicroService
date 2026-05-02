using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    public static class VergiHesabiHelper
    {
        public static decimal ToplamIlaveler(VergiHesaplama m)
            => m.Kkeg + m.KkegIstisna;

        public static decimal GecmisYilToplam(VergiHesaplama m)
            => m.GecmisYil_2024 + m.GecmisYil_2023 + m.GecmisYil_2022 + m.GecmisYil_2021;

        public static decimal ToplamIndirim(VergiHesaplama m)
            => GecmisYilToplam(m) + m.TemettuGeliri + m.BagisYardim;

        public static decimal? MaliKar(decimal? ticariKar, VergiHesaplama m)
        {
            if (!ticariKar.HasValue) return null;
            return ticariKar.Value + ToplamIlaveler(m) - ToplamIndirim(m);
        }

        public static decimal? HesaplananKv25(decimal? maliKar)
        {
            if (!maliKar.HasValue) return null;
            return maliKar.Value > 0
                ? maliKar.Value * TaxConstants.KurumlarVergisiOrani
                : 0m;
        }

        /// <summary>
        /// 691 hesabına yazılacak (gelir tablosunda -1 ile çarpılır).
        /// Hesaplanan KV %25 - %5 İndirim, asla negatif olmaz.
        /// </summary>
        public static decimal? HesaplananVergi691(decimal? hesaplananKv25, VergiHesaplama m)
        {
            if (!hesaplananKv25.HasValue) return null;
            var v = hesaplananKv25.Value - m.Kv5Indirim;
            return v > 0 ? v : 0m;
        }

        public static decimal ToplamTevkifat(VergiHesaplama m)
            => m.GeciciVergi + m.BankaStopaji + m.DigerTevkifat;

        /// <summary>
        /// Pozitif: ödenecek (yeşil); Negatif: iade çıkıyor (kırmızı).
        /// </summary>
        public static decimal? OdenecekVergi(decimal? hesaplananVergi691, VergiHesaplama m)
        {
            if (!hesaplananVergi691.HasValue) return null;
            return hesaplananVergi691.Value - ToplamTevkifat(m);
        }
    }
}
