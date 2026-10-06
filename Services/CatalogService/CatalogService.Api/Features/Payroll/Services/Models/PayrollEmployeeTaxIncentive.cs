using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Services.Models
{
    // Çalışan tarafı vergi teşviki: AÜ GV istisnasından sonra kalan vergiye uygulanan oran + DV istisnası.
    // Kanun kodundan yalnızca Resolve ile çözülür; motor kanun kodunu bilmez.
    public class PayrollEmployeeTaxIncentive
    {
        public static readonly PayrollEmployeeTaxIncentive None = new();

        public decimal IncomeTaxIncentiveRate { get; init; }
        public bool IsStampTaxExempt { get; init; }

        // 4691 Teknopark: GV %100, DV istisna
        private const decimal Law04691IncomeTaxRate = 1.00m;

        // 5746 Ar-Ge merkezi: eğitim durumuna göre GV oranı, DV istisna
        private const decimal Law05746DoktoraRate = 0.95m;
        private const decimal Law05746YuksekLisansRate = 0.90m;
        private const decimal Law05746DigerRate = 0.80m;

        public static PayrollEmployeeTaxIncentive Resolve(string lawCode, PayrollArGeEgitimDurumu egitimDurumu) => lawCode switch
        {
            "04691" => new() { IncomeTaxIncentiveRate = Law04691IncomeTaxRate, IsStampTaxExempt = true },
            "05746" => new()
            {
                IncomeTaxIncentiveRate = egitimDurumu switch
                {
                    PayrollArGeEgitimDurumu.Doktora95 => Law05746DoktoraRate,
                    PayrollArGeEgitimDurumu.YuksekLisans90 => Law05746YuksekLisansRate,
                    _ => Law05746DigerRate
                },
                IsStampTaxExempt = true
            },
            _ => None
        };
    }
}
