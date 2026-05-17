using System.Globalization;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Features.KdvBeyanname.Dtos;

namespace CatalogService.Api.Features.KdvBeyanname.Services.BdpXml
{
    // KdvSonucDto + Firma + Düzenleyen → XML için gereken tüm dinamik
    // değerleri toplayan ara model. Builder bunu okuyup XDocument'e yazar.
    public class BdpXmlModel
    {
        // Idari
        public string VdKodu { get; set; } = string.Empty;
        public int Yil { get; set; }
        public int Ay { get; set; }
        public DateTime DonemBaslangic { get; set; }
        public DateTime DonemBitis { get; set; }

        // Mükellef = Firma
        public KisiBlok Mukellef { get; set; } = new();

        // HSV (hesap sahibi vekili) — şimdilik mükellef ile aynı, sifat="kendisi"
        public KisiBlok Hsv { get; set; } = new();

        // Düzenleyen — app_settings'ten
        public KisiBlok Duzenleyen { get; set; } = new();

        // ─── Özel bölüm değerleri ─────────────────────────────────────────
        public List<TevkifatUygulanmayanSatir> TevkifatUygulanmayanlar { get; set; } = new();
        public string VergiToplami { get; set; } = "0.00";
        public string ToplamMatrah { get; set; } = "0.00";
        public string HesaplananKDV { get; set; } = "0.00";
        public string ToplamKDV { get; set; } = "0.00";

        public List<IndirilecekKdvOdSatir> IndirilecekKDVODler { get; set; } = new();
        public string IndirilecekKDVODToplamKDV { get; set; } = "0.00";

        public string OdenmesiGerekenKDV { get; set; } = "0.00";
        public string SonrakiDonemeDevredenKDV { get; set; } = "0.00";

        public string TeslimVeHizmetleriTeskilEdenBedelAylik { get; set; } = "0.00";
        public string TeslimVeHizmetleriTeskilEdenBedelKumulatif { get; set; } = "0";
    }

    public class KisiBlok
    {
        public string VergiNo { get; set; } = string.Empty;
        public string Adi { get; set; } = string.Empty;
        public string Soyadi { get; set; } = string.Empty;
        public string TicSicilNo { get; set; } = string.Empty;
        public string Eposta { get; set; } = string.Empty;
        public string AlanKodu { get; set; } = string.Empty;
        public string TelNo { get; set; } = string.Empty;
    }

    public class TevkifatUygulanmayanSatir
    {
        public string IslemTuru { get; set; } = BdpXmlConfig.TevkifatUygulanmayanIslemTuruDefault;
        public string Matrah { get; set; } = "0.00";
        public string Oran { get; set; } = "0";
        public string Vergi { get; set; } = "0.00";
    }

    public class IndirilecekKdvOdSatir
    {
        public string Oran { get; set; } = "0";
        public string Bedel { get; set; } = "0.00";
        public string KDVTutari { get; set; } = "0.00";
    }

    public interface IBdpXmlMapper
    {
        BdpXmlModel Map(Firma firma, Duzenleyen duzenleyen, KdvSonucDto sonuc);
    }

    public class BdpXmlMapper : IBdpXmlMapper
    {
        // Tutarları "478361.50" formatında (nokta ondalık, daima 2 ondalık) basar.
        private static string Money(decimal v) =>
            v.ToString("0.00", CultureInfo.InvariantCulture);

        // Tamsayı tutarlar (oran gibi) için.
        private static string Int(int v) =>
            v.ToString(CultureInfo.InvariantCulture);

        public BdpXmlModel Map(Firma firma, Duzenleyen duzenleyen, KdvSonucDto sonuc)
        {
            var donemBaslangic = new DateTime(sonuc.Yil, sonuc.Ay, 1);
            var donemBitis = donemBaslangic.AddMonths(1).AddDays(-1);

            var model = new BdpXmlModel
            {
                VdKodu         = firma.VergiDairesiKodu?.Trim() ?? string.Empty,
                Yil            = sonuc.Yil,
                Ay             = sonuc.Ay,
                DonemBaslangic = donemBaslangic,
                DonemBitis     = donemBitis,
            };

            var firmaBlok = new KisiBlok
            {
                VergiNo    = firma.VergiKimlikNo,
                Adi        = firma.YetkiliAdi    ?? string.Empty,
                Soyadi     = firma.YetkiliSoyadi ?? string.Empty,
                TicSicilNo = firma.TicaretSicilNo,
                Eposta     = firma.Email,
                AlanKodu   = firma.TelefonAlanKodu ?? string.Empty,
                TelNo      = firma.Telefon
            };

            // <hsv sifat="kendisi"> → mükellef ile aynı bilgileri
            model.Mukellef = firmaBlok;
            model.Hsv = firmaBlok;

            model.Duzenleyen = new KisiBlok
            {
                VergiNo    = duzenleyen.Vkn,
                Adi        = duzenleyen.Adi            ?? string.Empty,
                Soyadi     = duzenleyen.Soyadi         ?? string.Empty,
                TicSicilNo = duzenleyen.TicaretSicilNo ?? string.Empty,
                Eposta     = duzenleyen.Eposta         ?? string.Empty,
                AlanKodu   = duzenleyen.AlanKodu       ?? string.Empty,
                TelNo      = duzenleyen.TelNo          ?? string.Empty
            };

            // ── Özel bölüm: satış (tevkifat uygulanmayan) tarafı ──────────
            model.TevkifatUygulanmayanlar = sonuc.TevkifatUygulanmayanlar
                .Select(t => new TevkifatUygulanmayanSatir
                {
                    IslemTuru = BdpXmlConfig.TevkifatUygulanmayanIslemTuruDefault,
                    Matrah    = Money(t.Bedel),
                    Oran      = Int(t.Oran),
                    Vergi     = Money(t.KdvTutari)
                })
                .ToList();

            model.VergiToplami  = Money(sonuc.Hesaplanan391);
            model.ToplamMatrah  = Money(sonuc.Satislar600);
            model.HesaplananKDV = Money(sonuc.Hesaplanan391);
            model.ToplamKDV     = Money(sonuc.Hesaplanan391);

            // ── Özel bölüm: indirilecek KDV tarafı ────────────────────────
            model.IndirilecekKDVODler = sonuc.IndirilecekKdvODler
                .Select(i => new IndirilecekKdvOdSatir
                {
                    Oran      = Int(i.Oran),
                    Bedel     = Money(i.Bedel),
                    KDVTutari = Money(i.KdvTutari)
                })
                .ToList();
            model.IndirilecekKDVODToplamKDV = Money(sonuc.Indirilecek191);

            // ── Hesaplama sonuçları ───────────────────────────────────────
            model.OdenmesiGerekenKDV       = Money(sonuc.OdenmesiGerekenKDV);
            model.SonrakiDonemeDevredenKDV = Money(sonuc.SonrakiDonemeDevredenKDV);

            model.TeslimVeHizmetleriTeskilEdenBedelAylik     = Money(sonuc.Satislar600);
            model.TeslimVeHizmetleriTeskilEdenBedelKumulatif = Money(sonuc.Satislar600);

            return model;
        }
    }
}
