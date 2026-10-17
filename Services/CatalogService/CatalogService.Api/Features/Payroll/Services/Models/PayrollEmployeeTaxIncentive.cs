using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Services.Models
{
    // Çalışan tarafı teşvik: AÜ GV istisnasından sonra kalan vergiye uygulanan oran + DV istisnası
    // + (26322, 15921, 02828) sigortalı hissesinin karşılanması.
    // Kural tablosundan yalnızca Resolve ile çözülür; motor kanun kodunu bilmez.
    public class PayrollEmployeeTaxIncentive
    {
        public static readonly PayrollEmployeeTaxIncentive None = new();

        public decimal IncomeTaxIncentiveRate { get; init; }
        public bool IsStampTaxExempt { get; init; }

        /// <summary>GV ve DV teşvikinin uygulandığı brüt ücret üst sınırı (asgari ücret × 40). null: sınırsız.</summary>
        public decimal? WageCeiling { get; init; }

        /// <summary>Karşılanan sigortalı kalemleri (SGK / İSP), asgari ücret tabanında — net artar.</summary>
        public TesvikKalemleri EmployeeCoveredItems { get; init; }

        // 4691 Teknopark: GV %100
        private const decimal TamGvRate = 1.00m;

        // 5746 Ar-Ge / TEKMER: eğitim durumuna göre GV oranı
        private const decimal DoktoraRate = 0.95m;
        private const decimal YuksekLisansRate = 0.90m;
        private const decimal DigerRate = 0.80m;

        public static PayrollEmployeeTaxIncentive Resolve(
            CozulmusTesvik tesvik, PayrollArGeEgitimDurumu egitimDurumu, bool sigortaliKurallariUygulanir,
            decimal? ucretTavani = null)
        {
            var kural = tesvik.Kural;
            var sigortali = sigortaliKurallariUygulanir
                ? tesvik.Kalemler & TesvikKalemleri.Sigortali
                : TesvikKalemleri.Yok;

            if (kural.Gv == GvTesvikTuru.Yok && !kural.DvMuaf && sigortali == TesvikKalemleri.Yok)
                return None;

            return new()
            {
                IncomeTaxIncentiveRate = kural.Gv switch
                {
                    GvTesvikTuru.Tam => TamGvRate,
                    GvTesvikTuru.EgitimeGore => egitimDurumu switch
                    {
                        PayrollArGeEgitimDurumu.Doktora95 => DoktoraRate,
                        PayrollArGeEgitimDurumu.YuksekLisans90 => YuksekLisansRate,
                        _ => DigerRate
                    },
                    _ => 0m
                },
                IsStampTaxExempt = kural.DvMuaf,
                WageCeiling = kural.Gv != GvTesvikTuru.Yok || kural.DvMuaf ? ucretTavani : null,
                EmployeeCoveredItems = sigortali
            };
        }
    }
}
