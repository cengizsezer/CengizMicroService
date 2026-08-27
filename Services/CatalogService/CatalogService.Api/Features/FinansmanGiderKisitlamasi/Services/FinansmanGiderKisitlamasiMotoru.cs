using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Dtos;

namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services
{
    /// <summary>
    /// Finansman gider kısıtlaması (KVK 11/1-i, GVK 41/9) hesabı. Saf fonksiyon:
    /// veritabanı bilmez, hiçbir şey yazmaz; dört girdi + yılın oranını alır, dokuz
    /// satırı döner.
    ///
    /// <code>
    /// 1  Özsermaye                                (giriş, negatifse 0)
    /// 2  Yabancı kaynak toplamı                   (giriş)
    /// 3  Özsermayeyi aşan yabancı kaynak    = 2 − 1
    /// 4  Aşan kısmın yabancı kaynağa oranı  = 3 ÷ 2      (yüzde)
    /// 5  Finansman gider tutarı                   (giriş)
    /// 6  Örtülü sermaye gideri / finansman geliri (giriş)
    /// 7  Dikkate alınacak finansman gideri  = 5 − 6      (negatifse 0)
    /// 8  Aşan kısma isabet eden gider       = 4 × 7
    /// 9  KKEG olacak finansman gideri       = 8 × kısıtlama oranı
    /// </code>
    ///
    /// 3. satır sıfır veya negatifse — özsermaye yabancı kaynağa eşit ya da ondan büyükse —
    /// kısıtlama yapılmaz: 4–9 arası satırlar sıfır döner ve <see cref="FinansmanKisitlamaSonucDto.Aciklama"/>
    /// gerekçeyi taşır. 3. satırın kendisi sıfırlanmaz, ham fark olarak durur; kullanıcı
    /// özsermayenin yabancı kaynağı ne kadar aştığını görebilsin.
    /// </summary>
    public static class FinansmanGiderKisitlamasiMotoru
    {
        public sealed class Girdi
        {
            public int Yil { get; init; }
            public decimal Ozsermaye { get; init; }
            public decimal YabanciKaynakToplami { get; init; }
            public decimal FinansmanGideri { get; init; }
            public decimal OrtuluSermayeVeFinansmanGeliri { get; init; }

            /// <summary>
            /// Yılın kısıtlama oranı, <b>yüzde</b> (10 = %10). Tanımlı değilse null gelir ve
            /// hesap <see cref="FinansmanKisitlamaOraniYokException"/> ile durur.
            /// </summary>
            public decimal? KisitlamaOrani { get; init; }
        }

        public const string KisitlamaYokAciklamasi =
            "Yabancı kaynak özsermayeyi aşmıyor, gider kısıtlaması yapılmaz.";

        public static FinansmanKisitlamaSonucDto Hesapla(Girdi girdi)
        {
            if (girdi.KisitlamaOrani is not decimal oran)
                throw new FinansmanKisitlamaOraniYokException(girdi.Yil);

            // 1 — negatif özsermaye ile hesap yapılmaz; sıfır kabul edilir.
            var ozsermaye = girdi.Ozsermaye < 0 ? 0m : girdi.Ozsermaye;
            var yabanciKaynak = girdi.YabanciKaynakToplami;

            var sonuc = new FinansmanKisitlamaSonucDto
            {
                Yil = girdi.Yil,
                KisitlamaOrani = Yuvarla(oran),
                Ozsermaye = Yuvarla(ozsermaye),
                YabanciKaynakToplami = Yuvarla(yabanciKaynak),
                AsanYabanciKaynak = Yuvarla(yabanciKaynak - ozsermaye),
                FinansmanGideri = Yuvarla(girdi.FinansmanGideri),
                OrtuluSermayeVeFinansmanGeliri = Yuvarla(girdi.OrtuluSermayeVeFinansmanGeliri),
                // 7 — finansman geliri gideri aşarsa negatif değil sıfır.
                DikkateAlinacakFinansmanGideri =
                    Yuvarla(Math.Max(0m, girdi.FinansmanGideri - girdi.OrtuluSermayeVeFinansmanGeliri))
            };

            var asan = yabanciKaynak - ozsermaye;
            if (asan <= 0)
            {
                // 4–9 sıfır kalır (DTO varsayılanı); kullanıcıya gerekçe yazılır.
                sonuc.KisitlamaVar = false;
                sonuc.Aciklama = KisitlamaYokAciklamasi;
                return sonuc;
            }

            sonuc.KisitlamaVar = true;

            // 4 — yabancı kaynak sıfırsa bölme yapılmaz. (asan > 0 iken yabancı kaynak
            // ancak özsermaye negatif/sıfırken sıfır olabilir; yine de bölme korunuyor.)
            var asanOran = yabanciKaynak == 0 ? 0m : asan / yabanciKaynak;

            sonuc.AsanKisimOrani = Yuvarla(asanOran * 100m);

            // 8 ve 9 tam hassasiyetli oranla çarpılıp sonuçta yuvarlanıyor; 4. satırın
            // ekrandaki iki haneli hâliyle çarpmak kuruş farkı üretirdi (KARARLAR §80).
            var asanKismaIsabetEden = asanOran * sonuc.DikkateAlinacakFinansmanGideri;
            sonuc.AsanKismaIsabetEdenGider = Yuvarla(asanKismaIsabetEden);
            sonuc.Kkeg = Yuvarla(asanKismaIsabetEden * oran / 100m);

            return sonuc;
        }

        private static decimal Yuvarla(decimal deger) => Math.Round(deger, 2, MidpointRounding.AwayFromZero);
    }
}
